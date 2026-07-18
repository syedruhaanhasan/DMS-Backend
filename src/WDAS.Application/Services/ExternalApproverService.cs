using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WDAS.Application;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Application.Notifications;
using WDAS.Application.Options;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;
using WDAS.Domain.Exceptions;

namespace WDAS.Application.Services;

public class ExternalApproverService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly INotificationDispatcher _notifications;
    private readonly ExternalApproverOptions _options;

    public ExternalApproverService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IClock clock,
        IJwtTokenService jwtTokenService,
        INotificationDispatcher notifications,
        IOptions<ExternalApproverOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _jwtTokenService = jwtTokenService;
        _notifications = notifications;
        _options = options.Value;
    }

    public async Task<ExternalApproverSessionDto> CreateExternalApproverAsync(
        CreateExternalApproverRequest request,
        CancellationToken cancellationToken = default)
    {
        var workflowStepId = IdParsing.ParseRequired(request.WorkflowStepId, "Workflow step id");
        var step = await _db.WorkflowSteps
            .Include(s => s.Document)
            .FirstOrDefaultAsync(s => s.Id == workflowStepId, cancellationToken)
            ?? throw new DomainException("Workflow step not found.");

        if (step.Document.OwnerUserId != _currentUser.UserId &&
            !_currentUser.IsInRole(RoleNames.SuperAdmin) &&
            !_currentUser.IsInRole(RoleNames.DepartmentAdmin))
        {
            throw new DomainException("You are not authorized to add external approvers.");
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var now = _clock.UtcNow;

        var session = new ExternalApproverSession
        {
            WorkflowStepId = step.Id,
            ApproverName = request.ApproverName,
            ApproverEmail = request.ApproverEmail,
            SecureTokenHash = Hash(token),
            OtpHash = Hash(otp),
            LinkExpiresAtUtc = now.AddHours(_options.LinkExpiryHours),
            OtpExpiresAtUtc = now.AddMinutes(_options.OtpExpiryMinutes),
            CreatedAtUtc = now
        };

        _db.Add(session);
        await SaveAsync(cancellationToken);

        await SendInvitationEmailAsync(
            request.ApproverName,
            request.ApproverEmail,
            step.Document.Subject,
            token,
            otp,
            isResend: false,
            step.DocumentId,
            step.Id,
            cancellationToken);

        return new ExternalApproverSessionDto(
            IdParsing.ToApi(session.Id),
            IdParsing.ToApi(session.WorkflowStepId),
            session.ApproverEmail,
            session.LinkExpiresAtUtc,
            token);
    }

    public async Task<IReadOnlyList<ExternalApproverListItemDto>> ListExternalApproversAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsInRole(RoleNames.SuperAdmin) &&
            !_currentUser.IsInRole(RoleNames.DepartmentAdmin))
        {
            throw new DomainException("You are not authorized to list external approvers.");
        }

        var sessions = await _db.ExternalApproverSessions
            .Include(s => s.WorkflowStep)
                .ThenInclude(st => st.Document)
            .Include(s => s.WorkflowStep)
                .ThenInclude(st => st.Actions)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        return sessions.Select(s =>
        {
            var lastAction = s.WorkflowStep.Actions
                .OrderByDescending(a => a.ActionAtUtc)
                .FirstOrDefault(a => a.ActionType is WorkflowActionType.Approve or WorkflowActionType.Reject);

            var actionTaken = lastAction?.ActionType switch
            {
                WorkflowActionType.Approve => "approved",
                WorkflowActionType.Reject => "rejected",
                _ => s.OtpVerified ? "pending" : "pending"
            };

            return new ExternalApproverListItemDto(
                IdParsing.ToApi(s.Id),
                IdParsing.ToApi(s.WorkflowStepId),
                IdParsing.ToApi(s.WorkflowStep.DocumentId),
                s.WorkflowStep.Document.Subject,
                s.ApproverName,
                s.ApproverEmail,
                s.CreatedAtUtc,
                s.LinkExpiresAtUtc,
                s.OtpVerified,
                s.IsRevoked,
                actionTaken);
        }).ToList();
    }

    public async Task<ExternalApproverSessionDto> ResendLinkAsync(
        int sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.ExternalApproverSessions
            .Include(s => s.WorkflowStep)
                .ThenInclude(st => st.Document)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            ?? throw new DomainException("External approver session not found.");

        if (session.WorkflowStep.Document.OwnerUserId != _currentUser.UserId &&
            !_currentUser.IsInRole(RoleNames.SuperAdmin) &&
            !_currentUser.IsInRole(RoleNames.DepartmentAdmin))
        {
            throw new DomainException("You are not authorized to resend this link.");
        }

        if (session.IsRevoked)
        {
            throw new DomainException("This external approver session has been revoked.");
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var now = _clock.UtcNow;

        session.SecureTokenHash = Hash(token);
        session.OtpHash = Hash(otp);
        session.OtpVerified = false;
        session.VerifiedAtUtc = null;
        session.VerifiedFromIp = null;
        session.LinkExpiresAtUtc = now.AddHours(_options.LinkExpiryHours);
        session.OtpExpiresAtUtc = now.AddMinutes(_options.OtpExpiryMinutes);
        session.UpdatedAtUtc = now;

        await SaveAsync(cancellationToken);

        await SendInvitationEmailAsync(
            session.ApproverName,
            session.ApproverEmail,
            session.WorkflowStep.Document.Subject,
            token,
            otp,
            isResend: true,
            session.WorkflowStep.DocumentId,
            session.WorkflowStepId,
            cancellationToken);

        return new ExternalApproverSessionDto(
            IdParsing.ToApi(session.Id),
            IdParsing.ToApi(session.WorkflowStepId),
            session.ApproverEmail,
            session.LinkExpiresAtUtc,
            token);
    }

    public async Task<ExternalSessionDto> VerifyOtpAsync(VerifyExternalOtpRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = Hash(request.SecureLinkToken);
        var session = await _db.ExternalApproverSessions
            .Include(s => s.WorkflowStep)
            .FirstOrDefaultAsync(s => s.SecureTokenHash == tokenHash && !s.IsRevoked, cancellationToken)
            ?? throw new DomainException("Invalid or expired secure link.");

        var now = _clock.UtcNow;
        if (now > session.LinkExpiresAtUtc)
        {
            throw new DomainException("expired: Secure link has expired.");
        }

        if (session.OtpExpiresAtUtc.HasValue && now > session.OtpExpiresAtUtc.Value)
        {
            throw new DomainException("expired: OTP has expired.");
        }

        if (session.OtpHash != Hash(request.Otp))
        {
            throw new DomainException("Invalid OTP.");
        }

        session.OtpVerified = true;
        session.VerifiedAtUtc = now;
        session.VerifiedFromIp = request.ClientIp;
        session.UpdatedAtUtc = now;

        await SaveAsync(cancellationToken);

        var accessToken = _jwtTokenService.CreateExternalSessionToken(
            session.Id,
            session.WorkflowStepId,
            session.WorkflowStep.DocumentId,
            session.LinkExpiresAtUtc);

        return new ExternalSessionDto(
            accessToken,
            IdParsing.ToApi(session.WorkflowStepId),
            IdParsing.ToApi(session.WorkflowStep.DocumentId),
            session.LinkExpiresAtUtc);
    }

    private string BuildSecureLinkUrl(string token) =>
        $"{_options.AppBaseUrl.TrimEnd('/')}/external/verify?token={Uri.EscapeDataString(token)}";

    private async Task SendInvitationEmailAsync(
        string approverName,
        string approverEmail,
        string documentSubject,
        string token,
        string otp,
        bool isResend,
        int documentId,
        int workflowStepId,
        CancellationToken cancellationToken)
    {
        var secureLink = BuildSecureLinkUrl(token);
        var (subject, plainText, html) = ExternalInvitationEmailBuilder.Build(
            approverName,
            documentSubject,
            secureLink,
            otp,
            _options.OtpExpiryMinutes,
            _options.LinkExpiryHours,
            isResend);

        await _notifications.DispatchAsync(new NotificationRequest(
            NotificationEventType.ExternalOtp,
            null,
            approverEmail,
            documentId,
            workflowStepId,
            subject,
            plainText,
            html,
            Channels: [NotificationChannel.Email]),
            cancellationToken);
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_db is IUnitOfWork unitOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

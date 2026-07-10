using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WDAS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false),
                    PreviousHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntryHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorDisplayName = table.Column<string>(type: "text", nullable: true),
                    ActorEmail = table.Column<string>(type: "text", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<string>(type: "text", nullable: true),
                    EntityId = table.Column<string>(type: "text", nullable: true),
                    Action = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DetailsJson = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    ClientType = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdObjectId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    ParentDepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Departments_Departments_ParentDepartmentId",
                        column: x => x.ParentDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdObjectId = table.Column<string>(type: "text", nullable: false),
                    UserPrincipalName = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEnabledInAd = table.Column<bool>(type: "boolean", nullable: false),
                    IsDisabledInApp = table.Column<bool>(type: "boolean", nullable: false),
                    AdDisabledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Users_Users_ManagerUserId",
                        column: x => x.ManagerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Workflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DocumentType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workflows_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Delegations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DelegateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Delegations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Delegations_Users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Delegations_Users_DelegateUserId",
                        column: x => x.DelegateUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PushDeviceRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    DeviceToken = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushDeviceRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PushDeviceRegistrations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoleMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleMappings_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RoleMappings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ApprovalMode = table.Column<int>(type: "integer", nullable: false),
                    ReturnResumePolicy = table.Column<int>(type: "integer", nullable: false),
                    SlaThresholdHours = table.Column<int>(type: "integer", nullable: true),
                    EscalationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultRecipientsJson = table.Column<string>(type: "text", nullable: true),
                    NotificationSettingsJson = table.Column<string>(type: "text", nullable: true),
                    ActivatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowVersions_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalMatrixTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    MinAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ApproverUserIdsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalMatrixTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalMatrixTiers_WorkflowVersions_WorkflowVersionId",
                        column: x => x.WorkflowVersionId,
                        principalTable: "WorkflowVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApproverGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    Requirement = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApproverGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApproverGroups_WorkflowVersions_WorkflowVersionId",
                        column: x => x.WorkflowVersionId,
                        principalTable: "WorkflowVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToRecipients = table.Column<string>(type: "text", nullable: false),
                    FromDisplay = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BodyHtml = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsBodyLocked = table.Column<bool>(type: "boolean", nullable: false),
                    SubmitIdempotencyKey = table.Column<string>(type: "text", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AdHocApproverUserIdsJson = table.Column<string>(type: "text", nullable: true),
                    CancellationReason = table.Column<string>(type: "text", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchiveDocumentId = table.Column<string>(type: "text", nullable: true),
                    FinalizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Documents_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Documents_WorkflowVersions_WorkflowVersionId",
                        column: x => x.WorkflowVersionId,
                        principalTable: "WorkflowVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApproverGroupMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApproverGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApproverGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApproverGroupMembers_ApproverGroups_ApproverGroupId",
                        column: x => x.ApproverGroupId,
                        principalTable: "ApproverGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApproverGroupMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientName = table.Column<string>(type: "text", nullable: false),
                    RecipientEmail = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentRecipients_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentSearchIndexes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArchiveDocumentId = table.Column<string>(type: "text", nullable: true),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    OwnerDisplayName = table.Column<string>(type: "text", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SearchableText = table.Column<string>(type: "text", nullable: false),
                    IndexedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSearchIndexes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentSearchIndexes_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientEmail = table.Column<string>(type: "text", nullable: true),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowStepId = table.Column<Guid>(type: "uuid", nullable: true),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RepositoryDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArchiveDocumentId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FinalizedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinalizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    BodyHtmlSnapshot = table.Column<string>(type: "text", nullable: false),
                    ApprovalTrailJson = table.Column<string>(type: "text", nullable: false),
                    ArchivePdfStorageKey = table.Column<string>(type: "text", nullable: true),
                    IsImmutable = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryDocuments_Documents_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RepositoryDocuments_Users_FinalizedByUserId",
                        column: x => x.FinalizedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApproverGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroupName = table.Column<string>(type: "text", nullable: true),
                    GroupRequirement = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SlaDueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsSlaBreached = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresReconfirmation = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowSteps_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowSteps_Users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowSteps_WorkflowVersions_WorkflowVersionId",
                        column: x => x.WorkflowVersionId,
                        principalTable: "WorkflowVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalApproverSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApproverName = table.Column<string>(type: "text", nullable: false),
                    ApproverEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SecureTokenHash = table.Column<string>(type: "text", nullable: false),
                    OtpHash = table.Column<string>(type: "text", nullable: true),
                    LinkExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OtpExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OtpVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedFromIp = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalApproverSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalApproverSessions_WorkflowSteps_WorkflowStepId",
                        column: x => x.WorkflowStepId,
                        principalTable: "WorkflowSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStepActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OnBehalfOfUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    ActionAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowStepActions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowStepActions_WorkflowSteps_WorkflowStepId",
                        column: x => x.WorkflowStepId,
                        principalTable: "WorkflowSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowStepActionId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LogicalName = table.Column<string>(type: "text", nullable: true),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    IsLatest = table.Column<bool>(type: "boolean", nullable: false),
                    ScanStatus = table.Column<int>(type: "integer", nullable: false),
                    PreviewStorageKey = table.Column<string>(type: "text", nullable: true),
                    PreviewContentType = table.Column<string>(type: "text", nullable: true),
                    DownloadRestricted = table.Column<bool>(type: "boolean", nullable: false),
                    SupersededAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachments_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Attachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attachments_WorkflowStepActions_WorkflowStepActionId",
                        column: x => x.WorkflowStepActionId,
                        principalTable: "WorkflowStepActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalMatrixTiers_WorkflowVersionId_SequenceOrder",
                table: "ApprovalMatrixTiers",
                columns: new[] { "WorkflowVersionId", "SequenceOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApproverGroupMembers_ApproverGroupId",
                table: "ApproverGroupMembers",
                column: "ApproverGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ApproverGroupMembers_UserId",
                table: "ApproverGroupMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApproverGroups_WorkflowVersionId_SequenceOrder",
                table: "ApproverGroups",
                columns: new[] { "WorkflowVersionId", "SequenceOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_DocumentId_LogicalName_VersionNumber",
                table: "Attachments",
                columns: new[] { "DocumentId", "LogicalName", "VersionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_UploadedByUserId",
                table: "Attachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_WorkflowStepActionId",
                table: "Attachments",
                column: "WorkflowStepActionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_CreatedAtUtc",
                table: "AuditLogEntries",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_DocumentId",
                table: "AuditLogEntries",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_SequenceNumber",
                table: "AuditLogEntries",
                column: "SequenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Delegations_ApproverUserId_StartsAtUtc_EndsAtUtc",
                table: "Delegations",
                columns: new[] { "ApproverUserId", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Delegations_DelegateUserId",
                table: "Delegations",
                column: "DelegateUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_AdObjectId",
                table: "Departments",
                column: "AdObjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                table: "Departments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_ParentDepartmentId",
                table: "Departments",
                column: "ParentDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRecipients_DocumentId",
                table: "DocumentRecipients",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DepartmentId",
                table: "Documents",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_OwnerUserId",
                table: "Documents",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_SubmitIdempotencyKey",
                table: "Documents",
                column: "SubmitIdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_WorkflowId",
                table: "Documents",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_WorkflowVersionId",
                table: "Documents",
                column: "WorkflowVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSearchIndexes_ArchiveDocumentId",
                table: "DocumentSearchIndexes",
                column: "ArchiveDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSearchIndexes_DocumentId",
                table: "DocumentSearchIndexes",
                column: "DocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalApproverSessions_SecureTokenHash",
                table: "ExternalApproverSessions",
                column: "SecureTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalApproverSessions_WorkflowStepId",
                table: "ExternalApproverSessions",
                column: "WorkflowStepId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_DocumentId",
                table: "Notifications",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId_Status",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PushDeviceRegistrations_UserId_DeviceToken",
                table: "PushDeviceRegistrations",
                columns: new[] { "UserId", "DeviceToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryDocuments_ArchiveDocumentId",
                table: "RepositoryDocuments",
                column: "ArchiveDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryDocuments_FinalizedByUserId",
                table: "RepositoryDocuments",
                column: "FinalizedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryDocuments_SourceDocumentId",
                table: "RepositoryDocuments",
                column: "SourceDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleMappings_DepartmentId",
                table: "RoleMappings",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleMappings_UserId_Role_DepartmentId",
                table: "RoleMappings",
                columns: new[] { "UserId", "Role", "DepartmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_AdObjectId",
                table: "Users",
                column: "AdObjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_DepartmentId",
                table: "Users",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ManagerUserId",
                table: "Users",
                column: "ManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserPrincipalName",
                table: "Users",
                column: "UserPrincipalName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_DepartmentId_DocumentType_Name",
                table: "Workflows",
                columns: new[] { "DepartmentId", "DocumentType", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepActions_ActorUserId",
                table: "WorkflowStepActions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepActions_WorkflowStepId",
                table: "WorkflowStepActions",
                column: "WorkflowStepId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_ApproverUserId",
                table: "WorkflowSteps",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_DocumentId_StepOrder",
                table: "WorkflowSteps",
                columns: new[] { "DocumentId", "StepOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_WorkflowVersionId",
                table: "WorkflowSteps",
                column: "WorkflowVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowVersions_WorkflowId_VersionNumber",
                table: "WorkflowVersions",
                columns: new[] { "WorkflowId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalMatrixTiers");

            migrationBuilder.DropTable(
                name: "ApproverGroupMembers");

            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "AuditLogEntries");

            migrationBuilder.DropTable(
                name: "Delegations");

            migrationBuilder.DropTable(
                name: "DocumentRecipients");

            migrationBuilder.DropTable(
                name: "DocumentSearchIndexes");

            migrationBuilder.DropTable(
                name: "ExternalApproverSessions");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PushDeviceRegistrations");

            migrationBuilder.DropTable(
                name: "RepositoryDocuments");

            migrationBuilder.DropTable(
                name: "RoleMappings");

            migrationBuilder.DropTable(
                name: "ApproverGroups");

            migrationBuilder.DropTable(
                name: "WorkflowStepActions");

            migrationBuilder.DropTable(
                name: "WorkflowSteps");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "WorkflowVersions");

            migrationBuilder.DropTable(
                name: "Workflows");

            migrationBuilder.DropTable(
                name: "Departments");
        }
    }
}

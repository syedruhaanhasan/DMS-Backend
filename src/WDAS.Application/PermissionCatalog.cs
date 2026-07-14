namespace WDAS.Application;

/// <summary>Stable permission keys used by API policies and the Roles UI.</summary>
public static class PermissionCatalog
{
    public static class Nav
    {
        public const string Dashboard = "nav.dashboard";
        public const string DeptDashboard = "nav.dept_dashboard";
        public const string Inbox = "nav.inbox";
        public const string Documents = "nav.documents";
        public const string DocumentsNew = "nav.documents_new";
        public const string Repository = "nav.repository";
        public const string Reports = "nav.reports";
        public const string Config = "nav.config";
        public const string Settings = "nav.settings";
    }

    public static class Config
    {
        public const string Departments = "config.departments";
        public const string DepartmentsMake = "config.departments.make";
        public const string DepartmentsCheck = "config.departments.check";

        public const string Users = "config.users";
        public const string UsersMake = "config.users.make";
        public const string UsersCheck = "config.users.check";

        public const string Roles = "config.roles";
        public const string RolesMake = "config.roles.make";
        public const string RolesCheck = "config.roles.check";

        public const string ActiveDirectory = "config.ad";
        public const string ActiveDirectoryMake = "config.ad.make";
        public const string ActiveDirectoryCheck = "config.ad.check";

        public const string Workflows = "config.workflows";
        public const string WorkflowsMake = "config.workflows.make";
        public const string WorkflowsCheck = "config.workflows.check";

        public const string DocumentTypes = "config.document_types";
        public const string DocumentTypesMake = "config.document_types.make";
        public const string DocumentTypesCheck = "config.document_types.check";

        public const string ApprovalModes = "config.approval_modes";
        public const string ApprovalModesMake = "config.approval_modes.make";
        public const string ApprovalModesCheck = "config.approval_modes.check";

        public const string ExternalApprovers = "config.external_approvers";
        public const string ExternalApproversMake = "config.external_approvers.make";
        public const string ExternalApproversCheck = "config.external_approvers.check";

        public const string Delegation = "config.delegation";
        public const string DelegationMake = "config.delegation.make";
        public const string DelegationCheck = "config.delegation.check";
    }

    public static class Actions
    {
        public const string UsersCreate = "users.create";
        public const string UsersEditRoles = "users.edit_roles";
        public const string WorkflowsCreate = "workflows.create";
        public const string WorkflowsPublish = "workflows.publish";
        public const string DepartmentsManage = "departments.manage";
        public const string DocumentTypesManage = "document_types.manage";
        public const string DocumentsCreate = "documents.create";
        public const string DocumentsApprove = "documents.approve";
        public const string DocumentsFinalize = "documents.finalize";
        public const string DocumentsCancel = "documents.cancel";
        public const string ReportsView = "reports.view";
        public const string DelegationManage = "delegation.manage";
    }

    /// <summary>Config modules with maker (propose/edit) and checker (approve/activate) rights.</summary>
    public static IReadOnlyList<(string Base, string Make, string Check, string Label)> ConfigModules { get; } =
    [
        (Config.Departments, Config.DepartmentsMake, Config.DepartmentsCheck, "Departments"),
        (Config.Users, Config.UsersMake, Config.UsersCheck, "Users"),
        (Config.Roles, Config.RolesMake, Config.RolesCheck, "Roles"),
        (Config.ActiveDirectory, Config.ActiveDirectoryMake, Config.ActiveDirectoryCheck, "Active Directory"),
        (Config.Workflows, Config.WorkflowsMake, Config.WorkflowsCheck, "Workflows"),
        (Config.DocumentTypes, Config.DocumentTypesMake, Config.DocumentTypesCheck, "Document types"),
        (Config.ApprovalModes, Config.ApprovalModesMake, Config.ApprovalModesCheck, "Approval modes"),
        (Config.ExternalApprovers, Config.ExternalApproversMake, Config.ExternalApproversCheck, "External approvers"),
        (Config.Delegation, Config.DelegationMake, Config.DelegationCheck, "Delegation (admin)"),
    ];

    public static IReadOnlyList<(string Key, string Group, string Label)> Definitions { get; } =
        BuildDefinitions();

    private static IReadOnlyList<(string Key, string Group, string Label)> BuildDefinitions()
    {
        var list = new List<(string Key, string Group, string Label)>
        {
            (Nav.Dashboard, "Navigation", "Dashboard"),
            (Nav.DeptDashboard, "Navigation", "Department dashboard"),
            (Nav.Inbox, "Navigation", "Approval inbox"),
            (Nav.Documents, "Navigation", "My documents"),
            (Nav.DocumentsNew, "Navigation", "New document"),
            (Nav.Repository, "Navigation", "Repository"),
            (Nav.Reports, "Navigation", "Reports"),
            (Nav.Config, "Navigation", "Configuration menu"),
            (Nav.Settings, "Navigation", "Settings"),
        };

        foreach (var m in ConfigModules)
        {
            list.Add((m.Base, "Configuration", $"{m.Label} (access)"));
            list.Add((m.Make, "Configuration", $"{m.Label} — Maker"));
            list.Add((m.Check, "Configuration", $"{m.Label} — Checker"));
        }

        list.AddRange(
        [
            (Actions.UsersCreate, "Actions", "Create users"),
            (Actions.UsersEditRoles, "Actions", "Edit user roles"),
            (Actions.WorkflowsCreate, "Actions", "Create workflows"),
            (Actions.WorkflowsPublish, "Actions", "Publish workflows"),
            (Actions.DepartmentsManage, "Actions", "Manage departments"),
            (Actions.DocumentTypesManage, "Actions", "Manage document types"),
            (Actions.DocumentsCreate, "Actions", "Create documents"),
            (Actions.DocumentsApprove, "Actions", "Approve / reject / return"),
            (Actions.DocumentsFinalize, "Actions", "Finalize documents"),
            (Actions.DocumentsCancel, "Actions", "Cancel documents"),
            (Actions.ReportsView, "Actions", "View reports"),
            (Actions.DelegationManage, "Actions", "Manage delegations"),
        ]);

        return list;
    }

    public static IReadOnlyList<string> AllKeys { get; } =
        Definitions.Select(d => d.Key).ToList();

    public static bool IsValid(string key) => AllKeys.Contains(key);

    /// <summary>
    /// Ensures parent navigation keys are present when child screen/action permissions are granted.
    /// Also expands maker/checker pairs and maps legacy manage/create/publish keys.
    /// </summary>
    public static IReadOnlyList<string> ExpandImplied(IEnumerable<string> keys)
    {
        var set = new HashSet<string>(keys.Where(IsValid), StringComparer.Ordinal);

        // Legacy action keys → maker/checker
        if (set.Contains(Actions.DepartmentsManage))
        {
            set.Add(Config.DepartmentsMake);
            set.Add(Config.DepartmentsCheck);
        }

        if (set.Contains(Actions.UsersCreate))
        {
            set.Add(Config.UsersMake);
        }

        if (set.Contains(Actions.UsersEditRoles))
        {
            set.Add(Config.UsersCheck);
        }

        if (set.Contains(Actions.WorkflowsCreate))
        {
            set.Add(Config.WorkflowsMake);
        }

        if (set.Contains(Actions.WorkflowsPublish))
        {
            set.Add(Config.WorkflowsCheck);
        }

        if (set.Contains(Actions.DocumentTypesManage))
        {
            set.Add(Config.DocumentTypesMake);
            set.Add(Config.DocumentTypesCheck);
        }

        if (set.Contains(Actions.DelegationManage))
        {
            set.Add(Config.DelegationMake);
            set.Add(Config.DelegationCheck);
        }

        foreach (var m in ConfigModules)
        {
            var hasMake = set.Contains(m.Make);
            var hasCheck = set.Contains(m.Check);
            var hasBase = set.Contains(m.Base);

            if (hasMake || hasCheck)
            {
                set.Add(m.Base);
            }
            else if (hasBase)
            {
                // Legacy screen-only grant meant full config access for that module.
                set.Add(m.Make);
                set.Add(m.Check);
            }

            // Keep legacy action keys satisfiable when new maker/checker keys are used.
            if (m.Base == Config.Departments && (set.Contains(m.Make) || set.Contains(m.Check)))
            {
                set.Add(Actions.DepartmentsManage);
            }

            if (m.Base == Config.Users)
            {
                if (set.Contains(m.Make)) set.Add(Actions.UsersCreate);
                if (set.Contains(m.Check)) set.Add(Actions.UsersEditRoles);
            }

            if (m.Base == Config.Workflows)
            {
                if (set.Contains(m.Make)) set.Add(Actions.WorkflowsCreate);
                if (set.Contains(m.Check)) set.Add(Actions.WorkflowsPublish);
            }

            if (m.Base == Config.DocumentTypes && (set.Contains(m.Make) || set.Contains(m.Check)))
            {
                set.Add(Actions.DocumentTypesManage);
            }

            if (m.Base == Config.Delegation && (set.Contains(m.Make) || set.Contains(m.Check)))
            {
                set.Add(Actions.DelegationManage);
            }
        }

        if (set.Any(k => k.StartsWith("config.", StringComparison.Ordinal)))
        {
            set.Add(Nav.Config);
            set.Add(Nav.Dashboard);
            set.Add(Nav.Settings);
        }

        if (set.Contains(Actions.ReportsView) || set.Contains(Nav.Reports))
        {
            set.Add(Nav.Reports);
            set.Add(Nav.Dashboard);
        }

        if (set.Contains(Actions.DocumentsCreate) || set.Contains(Actions.DocumentsFinalize) || set.Contains(Actions.DocumentsCancel))
        {
            set.Add(Nav.Documents);
            set.Add(Nav.DocumentsNew);
            set.Add(Nav.Dashboard);
        }

        if (set.Contains(Actions.DocumentsApprove))
        {
            set.Add(Nav.Inbox);
            set.Add(Nav.Dashboard);
        }

        if (set.Contains(Actions.DelegationManage) || set.Contains(Config.DelegationMake) || set.Contains(Config.DelegationCheck))
        {
            set.Add(Nav.Settings);
            set.Add(Nav.Dashboard);
        }

        if (set.Contains(Nav.DeptDashboard) || set.Contains(Nav.Inbox) || set.Contains(Nav.Documents) ||
            set.Contains(Nav.DocumentsNew) || set.Contains(Nav.Repository) || set.Contains(Nav.Config) ||
            set.Contains(Nav.Settings))
        {
            set.Add(Nav.Dashboard);
        }

        return set.OrderBy(k => k, StringComparer.Ordinal).ToList();
    }

    public static IReadOnlyList<string> ForLegacyRole(string roleCode) => roleCode switch
    {
        RoleNames.SuperAdmin => AllKeys,
        RoleNames.DepartmentAdmin => ExpandImplied(
        [
            Nav.Dashboard, Nav.DeptDashboard, Nav.Inbox, Nav.Repository, Nav.Reports, Nav.Settings,
            Actions.ReportsView, Actions.DelegationManage, Actions.DocumentsApprove,
        ]),
        RoleNames.MakerOwner => ExpandImplied(
        [
            Nav.Dashboard, Nav.Documents, Nav.DocumentsNew, Nav.Repository, Nav.Settings,
            Actions.DocumentsCreate, Actions.DocumentsFinalize, Actions.DocumentsCancel,
        ]),
        RoleNames.Approver => ExpandImplied(
        [
            Nav.Dashboard, Nav.Inbox, Nav.Repository, Nav.Settings,
            Actions.DocumentsApprove, Actions.DelegationManage,
        ]),
        RoleNames.Auditor => ExpandImplied(
        [
            Nav.Dashboard, Nav.Repository, Nav.Reports, Nav.Settings,
            Actions.ReportsView,
        ]),
        RoleNames.ItAdmin => ExpandImplied(
        [
            Nav.Dashboard, Nav.Config, Nav.Settings,
            Config.ActiveDirectoryMake, Config.ActiveDirectoryCheck,
            Config.UsersMake, Config.UsersCheck,
            Config.DepartmentsMake, Config.DepartmentsCheck,
        ]),
        _ => [],
    };
}

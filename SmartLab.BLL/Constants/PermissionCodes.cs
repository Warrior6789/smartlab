namespace SmartLab.BLL.Constants;

/// <summary>
/// Must match permissions.permission_code seeded in the database.
/// Usage: [Authorize(Policy = PermissionCodes.ComponentManage)] requires claim "permission" = "COMPONENT_MANAGE".
/// </summary>
public static class PermissionCodes
{
    public const string UserManage = "USER_MANAGE";
    public const string RoleManage = "ROLE_MANAGE";
    public const string AcademicManage = "ACADEMIC_MANAGE";
    public const string ProjectCreate = "PROJECT_CREATE";
    public const string ProjectProposalApprove = "PROJECT_PROPOSAL_APPROVE";
    public const string ComponentView = "COMPONENT_VIEW";
    public const string ComponentManage = "COMPONENT_MANAGE";
    public const string PurchaseManage = "PURCHASE_MANAGE";
    public const string InventoryAudit = "INVENTORY_AUDIT";
    public const string BorrowCreate = "BORROW_CREATE";
    public const string BorrowApprove = "BORROW_APPROVE";
    public const string BorrowIssue = "BORROW_ISSUE";
    public const string IssueResolve = "ISSUE_RESOLVE";
    public const string ReportView = "REPORT_VIEW";
    public const string AuditLogView = "AUDITLOG_VIEW";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        UserManage, RoleManage, AcademicManage, ProjectCreate, ProjectProposalApprove, ComponentView, ComponentManage, PurchaseManage,
        InventoryAudit, BorrowCreate, BorrowApprove, BorrowIssue, IssueResolve, ReportView, AuditLogView,
    };
}

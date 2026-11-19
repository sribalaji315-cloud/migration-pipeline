namespace Haworth.MdPipeline.Api.Services;

public static class Policies
{
    public const string Viewers = nameof(Viewers);
    public const string Analysts = nameof(Analysts);
    public const string Approvers = nameof(Approvers);
    public const string Administrators = nameof(Administrators);

    public const string Viewer = "Viewer";
    public const string Analyst = "Analyst";
    public const string Approver = "Approver";
    public const string Admin = "Admin";

    public static readonly string[] AllRoles = [Viewer, Analyst, Approver, Admin];
}

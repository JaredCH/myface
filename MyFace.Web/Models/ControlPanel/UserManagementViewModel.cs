using System;
using System.Collections.Generic;
using System.Globalization;
using MyFace.Core.Entities;
using MyFace.Services;

namespace MyFace.Web.Models.ControlPanel;

public class UserManagementViewModel
{
    public UserManagementViewModel(UserManagementDetail? detail, IReadOnlyList<ControlPanelAuditEntry> auditEntries, bool isAdmin)
    {
        Detail = detail;
        AuditEntries = auditEntries;
        IsAdmin = isAdmin;
    }

    public UserManagementDetail? Detail { get; }
    public IReadOnlyList<ControlPanelAuditEntry> AuditEntries { get; }
    public bool IsAdmin { get; }
    public bool HasUser => Detail != null;
    public bool IsActive => Detail?.User.IsActive ?? false;
    public string Role => Detail?.User.Role ?? "Unknown";

    public string StatusLabel
    {
        get
        {
            if (!HasUser)
            {
                return "Select a user";
            }

            if (!IsActive)
            {
                return "Deactivated";
            }

            if (Detail!.User.SuspendedUntil.HasValue && Detail.User.SuspendedUntil.Value > DateTime.UtcNow)
            {
                return $"Suspended until {FormatDate(Detail.User.SuspendedUntil)}";
            }

            return "Active";
        }
    }

    public string FormatDate(DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC"
            : "—";
    }

    public string PgpStatus => string.IsNullOrWhiteSpace(Detail?.User.PgpPublicKey) ? "Not verified" : "PGP verified";
}

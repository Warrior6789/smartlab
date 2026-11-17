using Microsoft.EntityFrameworkCore;
using SmartLab.DAL.Entities;

namespace SmartLab.DAL.Context;

public partial class AppDbContext
{
    /// <summary>
    /// audit_logs.is_success has DB default true, so EF would skip an explicit false on insert;
    /// ValueGeneratedNever makes EF always send the value.
    /// </summary>
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>()
            .Property(e => e.IsSuccess)
            .ValueGeneratedNever();
    }
}

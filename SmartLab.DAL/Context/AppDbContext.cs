using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SmartLab.DAL.Entities;

namespace SmartLab.DAL.Context;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<BorrowExtension> BorrowExtensions { get; set; }

    public virtual DbSet<BorrowItem> BorrowItems { get; set; }

    public virtual DbSet<BorrowRequest> BorrowRequests { get; set; }

    public virtual DbSet<Class> Classes { get; set; }

    public virtual DbSet<ClassStudent> ClassStudents { get; set; }

    public virtual DbSet<CompatibleComponent> CompatibleComponents { get; set; }

    public virtual DbSet<Component> Components { get; set; }

    public virtual DbSet<ComponentCategory> ComponentCategories { get; set; }

    public virtual DbSet<ComponentImage> ComponentImages { get; set; }

    public virtual DbSet<ComponentItem> ComponentItems { get; set; }

    public virtual DbSet<ComponentSpec> ComponentSpecs { get; set; }

    public virtual DbSet<ComponentTutorial> ComponentTutorials { get; set; }

    public virtual DbSet<Course> Courses { get; set; }

    public virtual DbSet<InstructorProfile> InstructorProfiles { get; set; }

    public virtual DbSet<InventoryAudit> InventoryAudits { get; set; }

    public virtual DbSet<IssueReport> IssueReports { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<Project> Projects { get; set; }

    public virtual DbSet<ProjectComponent> ProjectComponents { get; set; }

    public virtual DbSet<PurchaseOrder> PurchaseOrders { get; set; }

    public virtual DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Semester> Semesters { get; set; }

    public virtual DbSet<StorageCabinet> StorageCabinets { get; set; }

    public virtual DbSet<StudentProfile> StudentProfiles { get; set; }

    public virtual DbSet<Team> Teams { get; set; }

    public virtual DbSet<TeamMember> TeamMembers { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("audit_logs_pkey");

            entity.ToTable("audit_logs");

            entity.HasIndex(e => e.CreatedAt, "ix_audit_logs_created_at");

            entity.HasIndex(e => e.UserId, "ix_audit_logs_user_id");

            entity.Property(e => e.AuditLogId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("audit_log_id");
            entity.Property(e => e.Action)
                .HasMaxLength(50)
                .HasColumnName("action");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.EntityId)
                .HasMaxLength(50)
                .HasColumnName("entity_id");
            entity.Property(e => e.EntityName)
                .HasMaxLength(100)
                .HasColumnName("entity_name");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .HasColumnName("ip_address");
            entity.Property(e => e.IsSuccess)
                .HasDefaultValue(true)
                .HasColumnName("is_success");
            entity.Property(e => e.NewValue)
                .HasColumnType("jsonb")
                .HasColumnName("new_value");
            entity.Property(e => e.OldValue)
                .HasColumnType("jsonb")
                .HasColumnName("old_value");
            entity.Property(e => e.UserAgent)
                .HasMaxLength(500)
                .HasColumnName("user_agent");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("audit_logs_user_id_fkey");
        });

        modelBuilder.Entity<BorrowExtension>(entity =>
        {
            entity.HasKey(e => e.ExtensionId).HasName("borrow_extensions_pkey");

            entity.ToTable("borrow_extensions");

            entity.Property(e => e.ExtensionId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("extension_id");
            entity.Property(e => e.BorrowRequestId).HasColumnName("borrow_request_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.NewDueDate).HasColumnName("new_due_date");
            entity.Property(e => e.OldDueDate).HasColumnName("old_due_date");
            entity.Property(e => e.Reason)
                .HasMaxLength(500)
                .HasColumnName("reason");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Pending'::character varying")
                .HasColumnName("status");

            entity.HasOne(d => d.BorrowRequest).WithMany(p => p.BorrowExtensions)
                .HasForeignKey(d => d.BorrowRequestId)
                .HasConstraintName("borrow_extensions_borrow_request_id_fkey");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.BorrowExtensions)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("borrow_extensions_reviewed_by_fkey");
        });

        modelBuilder.Entity<BorrowItem>(entity =>
        {
            entity.HasKey(e => e.BorrowItemId).HasName("borrow_items_pkey");

            entity.ToTable("borrow_items");

            entity.HasIndex(e => e.ComponentItemId, "ix_borrow_items_component_item");

            entity.HasIndex(e => e.BorrowRequestId, "ix_borrow_items_request_id");

            entity.Property(e => e.BorrowItemId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("borrow_item_id");
            entity.Property(e => e.BorrowRequestId).HasColumnName("borrow_request_id");
            entity.Property(e => e.ComponentId).HasColumnName("component_id");
            entity.Property(e => e.ComponentItemId).HasColumnName("component_item_id");
            entity.Property(e => e.IssuedAt).HasColumnName("issued_at");
            entity.Property(e => e.ReturnCondition)
                .HasMaxLength(255)
                .HasColumnName("return_condition");
            entity.Property(e => e.ReturnedAt).HasColumnName("returned_at");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Pending'::character varying")
                .HasColumnName("status");

            entity.HasOne(d => d.BorrowRequest).WithMany(p => p.BorrowItems)
                .HasForeignKey(d => d.BorrowRequestId)
                .HasConstraintName("borrow_items_borrow_request_id_fkey");

            entity.HasOne(d => d.Component).WithMany(p => p.BorrowItems)
                .HasForeignKey(d => d.ComponentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("borrow_items_component_id_fkey");

            entity.HasOne(d => d.ComponentItem).WithMany(p => p.BorrowItems)
                .HasForeignKey(d => d.ComponentItemId)
                .HasConstraintName("borrow_items_component_item_id_fkey");
        });

        modelBuilder.Entity<BorrowRequest>(entity =>
        {
            entity.HasKey(e => e.BorrowRequestId).HasName("borrow_requests_pkey");

            entity.ToTable("borrow_requests");

            entity.HasIndex(e => e.RequestCode, "borrow_requests_request_code_key").IsUnique();

            entity.HasIndex(e => e.RequesterId, "ix_borrow_requests_requester_id");

            entity.HasIndex(e => e.Status, "ix_borrow_requests_status");

            entity.Property(e => e.BorrowRequestId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("borrow_request_id");
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
            entity.Property(e => e.ApproverId).HasColumnName("approver_id");
            entity.Property(e => e.BorrowDate).HasColumnName("borrow_date");
            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.DueDate).HasColumnName("due_date");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.Purpose)
                .HasMaxLength(500)
                .HasColumnName("purpose");
            entity.Property(e => e.RejectReason)
                .HasMaxLength(500)
                .HasColumnName("reject_reason");
            entity.Property(e => e.RequestCode)
                .HasMaxLength(30)
                .HasColumnName("request_code");
            entity.Property(e => e.RequestDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("request_date");
            entity.Property(e => e.RequesterId).HasColumnName("requester_id");
            entity.Property(e => e.ReturnedAt).HasColumnName("returned_at");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Pending'::character varying")
                .HasColumnName("status");

            entity.HasOne(d => d.Approver).WithMany(p => p.BorrowRequestApprovers)
                .HasForeignKey(d => d.ApproverId)
                .HasConstraintName("borrow_requests_approver_id_fkey");

            entity.HasOne(d => d.Class).WithMany(p => p.BorrowRequests)
                .HasForeignKey(d => d.ClassId)
                .HasConstraintName("borrow_requests_class_id_fkey");

            entity.HasOne(d => d.Project).WithMany(p => p.BorrowRequests)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("borrow_requests_project_id_fkey");

            entity.HasOne(d => d.Requester).WithMany(p => p.BorrowRequestRequesters)
                .HasForeignKey(d => d.RequesterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("borrow_requests_requester_id_fkey");
        });

        modelBuilder.Entity<Class>(entity =>
        {
            entity.HasKey(e => e.ClassId).HasName("classes_pkey");

            entity.ToTable("classes");

            entity.HasIndex(e => new { e.ClassCode, e.SemesterId }, "classes_class_code_semester_id_key").IsUnique();

            entity.HasIndex(e => e.CourseId, "ix_classes_course_id");

            entity.HasIndex(e => e.InstructorId, "ix_classes_instructor_id");

            entity.HasIndex(e => e.SemesterId, "ix_classes_semester_id");

            entity.Property(e => e.ClassId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("class_id");
            entity.Property(e => e.ClassCode)
                .HasMaxLength(20)
                .HasColumnName("class_code");
            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.InstructorId).HasColumnName("instructor_id");
            entity.Property(e => e.MaxStudents)
                .HasDefaultValue(30)
                .HasColumnName("max_students");
            entity.Property(e => e.SemesterId).HasColumnName("semester_id");

            entity.HasOne(d => d.Course).WithMany(p => p.Classes)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("classes_course_id_fkey");

            entity.HasOne(d => d.Instructor).WithMany(p => p.Classes)
                .HasForeignKey(d => d.InstructorId)
                .HasConstraintName("classes_instructor_id_fkey");

            entity.HasOne(d => d.Semester).WithMany(p => p.Classes)
                .HasForeignKey(d => d.SemesterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("classes_semester_id_fkey");
        });

        modelBuilder.Entity<ClassStudent>(entity =>
        {
            entity.HasKey(e => new { e.ClassId, e.StudentId }).HasName("class_students_pkey");

            entity.ToTable("class_students");

            entity.HasIndex(e => e.StudentId, "ix_class_students_student_id");

            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.EnrolledAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("enrolled_at");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Studying'::character varying")
                .HasColumnName("status");

            entity.HasOne(d => d.Class).WithMany(p => p.ClassStudents)
                .HasForeignKey(d => d.ClassId)
                .HasConstraintName("class_students_class_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.ClassStudents)
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("class_students_student_id_fkey");
        });

        modelBuilder.Entity<CompatibleComponent>(entity =>
        {
            entity.HasKey(e => new { e.ComponentId, e.CompatibleComponentId }).HasName("compatible_components_pkey");

            entity.ToTable("compatible_components");

            entity.Property(e => e.ComponentId).HasColumnName("component_id");
            entity.Property(e => e.CompatibleComponentId).HasColumnName("compatible_component_id");
            entity.Property(e => e.Note)
                .HasMaxLength(255)
                .HasColumnName("note");

            entity.HasOne(d => d.CompatibleComponentNavigation).WithMany(p => p.CompatibleComponentCompatibleComponentNavigations)
                .HasForeignKey(d => d.CompatibleComponentId)
                .HasConstraintName("compatible_components_compatible_component_id_fkey");

            entity.HasOne(d => d.Component).WithMany(p => p.CompatibleComponentComponents)
                .HasForeignKey(d => d.ComponentId)
                .HasConstraintName("compatible_components_component_id_fkey");
        });

        modelBuilder.Entity<Component>(entity =>
        {
            entity.HasKey(e => e.ComponentId).HasName("components_pkey");

            entity.ToTable("components");

            entity.HasIndex(e => e.ComponentCode, "components_component_code_key").IsUnique();

            entity.HasIndex(e => e.CategoryId, "ix_components_category_id");

            entity.Property(e => e.ComponentId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("component_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.ComponentCode)
                .HasMaxLength(50)
                .HasColumnName("component_code");
            entity.Property(e => e.ComponentName)
                .HasMaxLength(150)
                .HasColumnName("component_name");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DatasheetUrl)
                .HasMaxLength(500)
                .HasColumnName("datasheet_url");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Manufacturer)
                .HasMaxLength(100)
                .HasColumnName("manufacturer");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Category).WithMany(p => p.Components)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("components_category_id_fkey");
        });

        modelBuilder.Entity<ComponentCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("component_categories_pkey");

            entity.ToTable("component_categories");

            entity.Property(e => e.CategoryId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("category_id");
            entity.Property(e => e.CategoryName)
                .HasMaxLength(100)
                .HasColumnName("category_name");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
        });

        modelBuilder.Entity<ComponentImage>(entity =>
        {
            entity.HasKey(e => e.ImageId).HasName("component_images_pkey");

            entity.ToTable("component_images");

            entity.HasIndex(e => e.ComponentId, "ix_component_images_component_id");

            entity.Property(e => e.ImageId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("image_id");
            entity.Property(e => e.AnnotationUrl)
                .HasMaxLength(500)
                .HasColumnName("annotation_url");
            entity.Property(e => e.ComponentId).HasColumnName("component_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ImageType)
                .HasMaxLength(20)
                .HasColumnName("image_type");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500)
                .HasColumnName("image_url");
            entity.Property(e => e.IsPrimary)
                .HasDefaultValue(false)
                .HasColumnName("is_primary");
            entity.Property(e => e.StorageKey)
                .HasMaxLength(255)
                .HasColumnName("storage_key");
            entity.Property(e => e.UploadedBy).HasColumnName("uploaded_by");

            entity.HasOne(d => d.Component).WithMany(p => p.ComponentImages)
                .HasForeignKey(d => d.ComponentId)
                .HasConstraintName("component_images_component_id_fkey");

            entity.HasOne(d => d.UploadedByNavigation).WithMany(p => p.ComponentImages)
                .HasForeignKey(d => d.UploadedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("component_images_uploaded_by_fkey");
        });

        modelBuilder.Entity<ComponentItem>(entity =>
        {
            entity.HasKey(e => e.ItemId).HasName("component_items_pkey");

            entity.ToTable("component_items");

            entity.HasIndex(e => e.Barcode, "component_items_barcode_key").IsUnique();

            entity.HasIndex(e => e.CabinetId, "ix_component_items_cabinet_id");

            entity.HasIndex(e => e.ComponentId, "ix_component_items_component_id");

            entity.HasIndex(e => e.Status, "ix_component_items_status");

            entity.Property(e => e.ItemId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("item_id");
            entity.Property(e => e.Barcode)
                .HasMaxLength(100)
                .HasColumnName("barcode");
            entity.Property(e => e.CabinetId).HasColumnName("cabinet_id");
            entity.Property(e => e.ComponentId).HasColumnName("component_id");
            entity.Property(e => e.Condition)
                .HasMaxLength(255)
                .HasColumnName("condition");
            entity.Property(e => e.ImportedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("imported_at");
            entity.Property(e => e.PurchaseOrderItemId).HasColumnName("purchase_order_item_id");
            entity.Property(e => e.SerialNumber)
                .HasMaxLength(100)
                .HasColumnName("serial_number");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Available'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Cabinet).WithMany(p => p.ComponentItems)
                .HasForeignKey(d => d.CabinetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("component_items_cabinet_id_fkey");

            entity.HasOne(d => d.Component).WithMany(p => p.ComponentItems)
                .HasForeignKey(d => d.ComponentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("component_items_component_id_fkey");

            entity.HasOne(d => d.PurchaseOrderItem).WithMany(p => p.ComponentItems)
                .HasForeignKey(d => d.PurchaseOrderItemId)
                .HasConstraintName("component_items_purchase_order_item_id_fkey");
        });

        modelBuilder.Entity<ComponentSpec>(entity =>
        {
            entity.HasKey(e => e.SpecId).HasName("component_specs_pkey");

            entity.ToTable("component_specs");

            entity.HasIndex(e => e.ComponentId, "ix_component_specs_component_id");

            entity.Property(e => e.SpecId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("spec_id");
            entity.Property(e => e.ComponentId).HasColumnName("component_id");
            entity.Property(e => e.ConfidenceScore)
                .HasPrecision(5, 2)
                .HasColumnName("confidence_score");
            entity.Property(e => e.IsAiExtracted)
                .HasDefaultValue(false)
                .HasColumnName("is_ai_extracted");
            entity.Property(e => e.SpecKey)
                .HasMaxLength(100)
                .HasColumnName("spec_key");
            entity.Property(e => e.SpecValue)
                .HasMaxLength(255)
                .HasColumnName("spec_value");
            entity.Property(e => e.Unit)
                .HasMaxLength(20)
                .HasColumnName("unit");

            entity.HasOne(d => d.Component).WithMany(p => p.ComponentSpecs)
                .HasForeignKey(d => d.ComponentId)
                .HasConstraintName("component_specs_component_id_fkey");
        });

        modelBuilder.Entity<ComponentTutorial>(entity =>
        {
            entity.HasKey(e => e.TutorialId).HasName("component_tutorials_pkey");

            entity.ToTable("component_tutorials");

            entity.Property(e => e.TutorialId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("tutorial_id");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.ComponentId).HasColumnName("component_id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.VideoUrl)
                .HasMaxLength(500)
                .HasColumnName("video_url");

            entity.HasOne(d => d.Author).WithMany(p => p.ComponentTutorials)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("component_tutorials_author_id_fkey");

            entity.HasOne(d => d.Component).WithMany(p => p.ComponentTutorials)
                .HasForeignKey(d => d.ComponentId)
                .HasConstraintName("component_tutorials_component_id_fkey");
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(e => e.CourseId).HasName("courses_pkey");

            entity.ToTable("courses");

            entity.HasIndex(e => e.CourseCode, "courses_course_code_key").IsUnique();

            entity.Property(e => e.CourseId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("course_id");
            entity.Property(e => e.CourseCode)
                .HasMaxLength(20)
                .HasColumnName("course_code");
            entity.Property(e => e.CourseName)
                .HasMaxLength(150)
                .HasColumnName("course_name");
            entity.Property(e => e.Credits).HasColumnName("credits");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
        });

        modelBuilder.Entity<InstructorProfile>(entity =>
        {
            entity.HasKey(e => e.InstructorProfileId).HasName("instructor_profiles_pkey");

            entity.ToTable("instructor_profiles");

            entity.HasIndex(e => e.InstructorCode, "instructor_profiles_instructor_code_key").IsUnique();

            entity.HasIndex(e => e.UserId, "instructor_profiles_user_id_key").IsUnique();

            entity.Property(e => e.InstructorProfileId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("instructor_profile_id");
            entity.Property(e => e.AcademicTitle)
                .HasMaxLength(50)
                .HasColumnName("academic_title");
            entity.Property(e => e.Department)
                .HasMaxLength(100)
                .HasColumnName("department");
            entity.Property(e => e.InstructorCode)
                .HasMaxLength(20)
                .HasColumnName("instructor_code");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithOne(p => p.InstructorProfile)
                .HasForeignKey<InstructorProfile>(d => d.UserId)
                .HasConstraintName("instructor_profiles_user_id_fkey");
        });

        modelBuilder.Entity<InventoryAudit>(entity =>
        {
            entity.HasKey(e => e.AuditId).HasName("inventory_audits_pkey");

            entity.ToTable("inventory_audits");

            entity.Property(e => e.AuditId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("audit_id");
            entity.Property(e => e.AuditDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("audit_date");
            entity.Property(e => e.AuditorId).HasColumnName("auditor_id");
            entity.Property(e => e.CabinetId).HasColumnName("cabinet_id");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'InProgress'::character varying")
                .HasColumnName("status");

            entity.HasOne(d => d.Auditor).WithMany(p => p.InventoryAudits)
                .HasForeignKey(d => d.AuditorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("inventory_audits_auditor_id_fkey");

            entity.HasOne(d => d.Cabinet).WithMany(p => p.InventoryAudits)
                .HasForeignKey(d => d.CabinetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("inventory_audits_cabinet_id_fkey");
        });

        modelBuilder.Entity<IssueReport>(entity =>
        {
            entity.HasKey(e => e.IssueReportId).HasName("issue_reports_pkey");

            entity.ToTable("issue_reports");

            entity.HasIndex(e => e.ComponentItemId, "ix_issue_reports_component_item");

            entity.Property(e => e.IssueReportId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("issue_report_id");
            entity.Property(e => e.BorrowItemId).HasColumnName("borrow_item_id");
            entity.Property(e => e.CompensationAmount)
                .HasPrecision(18, 2)
                .HasColumnName("compensation_amount");
            entity.Property(e => e.ComponentItemId).HasColumnName("component_item_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500)
                .HasColumnName("image_url");
            entity.Property(e => e.IssueType)
                .HasMaxLength(20)
                .HasColumnName("issue_type");
            entity.Property(e => e.ReportedBy).HasColumnName("reported_by");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.ResolvedBy).HasColumnName("resolved_by");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Open'::character varying")
                .HasColumnName("status");

            entity.HasOne(d => d.BorrowItem).WithMany(p => p.IssueReports)
                .HasForeignKey(d => d.BorrowItemId)
                .HasConstraintName("issue_reports_borrow_item_id_fkey");

            entity.HasOne(d => d.ComponentItem).WithMany(p => p.IssueReports)
                .HasForeignKey(d => d.ComponentItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("issue_reports_component_item_id_fkey");

            entity.HasOne(d => d.ReportedByNavigation).WithMany(p => p.IssueReportReportedByNavigations)
                .HasForeignKey(d => d.ReportedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("issue_reports_reported_by_fkey");

            entity.HasOne(d => d.ResolvedByNavigation).WithMany(p => p.IssueReportResolvedByNavigations)
                .HasForeignKey(d => d.ResolvedBy)
                .HasConstraintName("issue_reports_resolved_by_fkey");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.PermissionId).HasName("permissions_pkey");

            entity.ToTable("permissions");

            entity.HasIndex(e => e.PermissionCode, "permissions_permission_code_key").IsUnique();

            entity.Property(e => e.PermissionId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("permission_id");
            entity.Property(e => e.Module)
                .HasMaxLength(50)
                .HasColumnName("module");
            entity.Property(e => e.PermissionCode)
                .HasMaxLength(100)
                .HasColumnName("permission_code");
            entity.Property(e => e.PermissionName)
                .HasMaxLength(100)
                .HasColumnName("permission_name");
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.ProjectId).HasName("projects_pkey");

            entity.ToTable("projects");

            entity.HasIndex(e => e.ClassId, "ix_projects_class_id");

            entity.HasIndex(e => e.OwnerId, "ix_projects_owner_id");

            entity.Property(e => e.ProjectId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("project_id");
            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DemoVideoUrl)
                .HasMaxLength(500)
                .HasColumnName("demo_video_url");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.GithubUrl)
                .HasMaxLength(500)
                .HasColumnName("github_url");
            entity.Property(e => e.OwnerId).HasColumnName("owner_id");
            entity.Property(e => e.SlideUrl)
                .HasMaxLength(500)
                .HasColumnName("slide_url");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Draft'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.TeamId).HasColumnName("team_id");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Class).WithMany(p => p.Projects)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("projects_class_id_fkey");

            entity.HasOne(d => d.Owner).WithMany(p => p.Projects)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("projects_owner_id_fkey");

            entity.HasOne(d => d.Team).WithMany(p => p.Projects)
                .HasForeignKey(d => d.TeamId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("projects_team_id_fkey");
        });

        modelBuilder.Entity<ProjectComponent>(entity =>
        {
            entity.HasKey(e => new { e.ProjectId, e.ComponentId }).HasName("project_components_pkey");

            entity.ToTable("project_components");

            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.ComponentId).HasColumnName("component_id");
            entity.Property(e => e.Note)
                .HasMaxLength(255)
                .HasColumnName("note");
            entity.Property(e => e.Quantity)
                .HasDefaultValue(1)
                .HasColumnName("quantity");

            entity.HasOne(d => d.Component).WithMany(p => p.ProjectComponents)
                .HasForeignKey(d => d.ComponentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("project_components_component_id_fkey");

            entity.HasOne(d => d.Project).WithMany(p => p.ProjectComponents)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("project_components_project_id_fkey");
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasKey(e => e.PurchaseOrderId).HasName("purchase_orders_pkey");

            entity.ToTable("purchase_orders");

            entity.HasIndex(e => e.OrderCode, "purchase_orders_order_code_key").IsUnique();

            entity.Property(e => e.PurchaseOrderId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("purchase_order_id");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.OrderCode)
                .HasMaxLength(30)
                .HasColumnName("order_code");
            entity.Property(e => e.OrderDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("order_date");
            entity.Property(e => e.ReceivedDate).HasColumnName("received_date");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Draft'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.SupplierName)
                .HasMaxLength(150)
                .HasColumnName("supplier_name");
            entity.Property(e => e.TotalAmount)
                .HasPrecision(18, 2)
                .HasColumnName("total_amount");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.PurchaseOrderApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("purchase_orders_approved_by_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PurchaseOrderCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("purchase_orders_created_by_fkey");
        });

        modelBuilder.Entity<PurchaseOrderItem>(entity =>
        {
            entity.HasKey(e => e.PurchaseOrderItemId).HasName("purchase_order_items_pkey");

            entity.ToTable("purchase_order_items");

            entity.Property(e => e.PurchaseOrderItemId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("purchase_order_item_id");
            entity.Property(e => e.ComponentId).HasColumnName("component_id");
            entity.Property(e => e.PurchaseOrderId).HasColumnName("purchase_order_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.ReceivedQuantity)
                .HasDefaultValue(0)
                .HasColumnName("received_quantity");
            entity.Property(e => e.SubTotal)
                .HasPrecision(18, 2)
                .HasColumnName("sub_total");
            entity.Property(e => e.UnitPrice)
                .HasPrecision(18, 2)
                .HasColumnName("unit_price");

            entity.HasOne(d => d.Component).WithMany(p => p.PurchaseOrderItems)
                .HasForeignKey(d => d.ComponentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("purchase_order_items_component_id_fkey");

            entity.HasOne(d => d.PurchaseOrder).WithMany(p => p.PurchaseOrderItems)
                .HasForeignKey(d => d.PurchaseOrderId)
                .HasConstraintName("purchase_order_items_purchase_order_id_fkey");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("roles_pkey");

            entity.ToTable("roles");

            entity.HasIndex(e => e.RoleName, "roles_role_name_key").IsUnique();

            entity.Property(e => e.RoleId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("role_id");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .HasColumnName("role_name");

            entity.HasMany(d => d.Permissions).WithMany(p => p.Roles)
                .UsingEntity<Dictionary<string, object>>(
                    "RolePermission",
                    r => r.HasOne<Permission>().WithMany()
                        .HasForeignKey("PermissionId")
                        .HasConstraintName("role_permissions_permission_id_fkey"),
                    l => l.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleId")
                        .HasConstraintName("role_permissions_role_id_fkey"),
                    j =>
                    {
                        j.HasKey("RoleId", "PermissionId").HasName("role_permissions_pkey");
                        j.ToTable("role_permissions");
                        j.HasIndex(new[] { "PermissionId" }, "ix_role_permissions_perm_id");
                        j.IndexerProperty<Guid>("RoleId").HasColumnName("role_id");
                        j.IndexerProperty<Guid>("PermissionId").HasColumnName("permission_id");
                    });
        });

        modelBuilder.Entity<Semester>(entity =>
        {
            entity.HasKey(e => e.SemesterId).HasName("semesters_pkey");

            entity.ToTable("semesters");

            entity.HasIndex(e => e.SemesterCode, "semesters_semester_code_key").IsUnique();

            entity.Property(e => e.SemesterId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("semester_id");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.SemesterCode)
                .HasMaxLength(10)
                .HasColumnName("semester_code");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.Term)
                .HasMaxLength(10)
                .HasColumnName("term");
            entity.Property(e => e.Year).HasColumnName("year");
        });

        modelBuilder.Entity<StorageCabinet>(entity =>
        {
            entity.HasKey(e => e.CabinetId).HasName("storage_cabinets_pkey");

            entity.ToTable("storage_cabinets");

            entity.HasIndex(e => e.CabinetCode, "storage_cabinets_cabinet_code_key").IsUnique();

            entity.Property(e => e.CabinetId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("cabinet_id");
            entity.Property(e => e.CabinetCode)
                .HasMaxLength(20)
                .HasColumnName("cabinet_code");
            entity.Property(e => e.CabinetName)
                .HasMaxLength(100)
                .HasColumnName("cabinet_name");
            entity.Property(e => e.LabRoom)
                .HasMaxLength(50)
                .HasColumnName("lab_room");
            entity.Property(e => e.Location)
                .HasMaxLength(255)
                .HasColumnName("location");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Active'::character varying")
                .HasColumnName("status");
        });

        modelBuilder.Entity<StudentProfile>(entity =>
        {
            entity.HasKey(e => e.StudentProfileId).HasName("student_profiles_pkey");

            entity.ToTable("student_profiles");

            entity.HasIndex(e => e.StudentCode, "student_profiles_student_code_key").IsUnique();

            entity.HasIndex(e => e.UserId, "student_profiles_user_id_key").IsUnique();

            entity.Property(e => e.StudentProfileId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("student_profile_id");
            entity.Property(e => e.Cohort)
                .HasMaxLength(10)
                .HasColumnName("cohort");
            entity.Property(e => e.Major)
                .HasMaxLength(100)
                .HasColumnName("major");
            entity.Property(e => e.StudentCode)
                .HasMaxLength(20)
                .HasColumnName("student_code");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithOne(p => p.StudentProfile)
                .HasForeignKey<StudentProfile>(d => d.UserId)
                .HasConstraintName("student_profiles_user_id_fkey");
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(e => e.TeamId).HasName("teams_pkey");

            entity.ToTable("teams");

            entity.HasIndex(e => new { e.ClassId, e.TeamName }, "teams_class_id_team_name_key").IsUnique();

            entity.Property(e => e.TeamId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("team_id");
            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.LeaderId).HasColumnName("leader_id");
            entity.Property(e => e.TeamName)
                .HasMaxLength(100)
                .HasColumnName("team_name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Class).WithMany(p => p.Teams)
                .HasForeignKey(d => d.ClassId)
                .HasConstraintName("teams_class_id_fkey");

            entity.HasOne(d => d.Leader).WithMany(p => p.Teams)
                .HasForeignKey(d => d.LeaderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("teams_leader_id_fkey");
        });

        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.HasKey(e => new { e.TeamId, e.StudentId }).HasName("team_members_pkey");

            entity.ToTable("team_members");

            entity.HasIndex(e => e.StudentId, "ix_team_members_student_id");

            entity.Property(e => e.TeamId).HasColumnName("team_id");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.InvitedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("invited_at");
            entity.Property(e => e.RespondedAt).HasColumnName("responded_at");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Pending'::character varying")
                .HasColumnName("status");

            entity.HasOne(d => d.Student).WithMany(p => p.TeamMembers)
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("team_members_student_id_fkey");

            entity.HasOne(d => d.Team).WithMany(p => p.TeamMembers)
                .HasForeignKey(d => d.TeamId)
                .HasConstraintName("team_members_team_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.HasIndex(e => e.Username, "users_username_key").IsUnique();

            entity.Property(e => e.UserId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("user_id");
            entity.Property(e => e.AvatarUrl)
                .HasMaxLength(500)
                .HasColumnName("avatar_url");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasColumnType("citext")
                .HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .HasColumnName("full_name");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(15)
                .HasColumnName("phone_number");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .HasColumnName("username");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId }).HasName("user_roles_pkey");

            entity.ToTable("user_roles");

            entity.HasIndex(e => e.RoleId, "ix_user_roles_role_id");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("assigned_at");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("user_roles_role_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("user_roles_user_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

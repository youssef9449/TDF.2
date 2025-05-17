using Microsoft.EntityFrameworkCore;
using TDFShared.Models.User;
using TDFShared.Models.Message;
using TDFShared.Models.Notification;
using TDFShared.Models.Request;
using TDFAPI.Domain.Auth;

namespace TDFAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }

        public DbSet<User> Users { get; set; }
        public DbSet<RequestEntity> Requests { get; set; }
        public DbSet<NotificationEntity> Notifications { get; set; }
        public DbSet<MessageEntity> Messages { get; set; }
        public DbSet<AnnualLeaveEntity> AnnualLeaves { get; set; }
        public DbSet<RevokedToken> RevokedTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Explicitly configure that UserDto is not an entity type
            builder.Ignore<TDFShared.DTOs.Users.UserDto>();

            builder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.UserID);
                entity.Property(e => e.UserID).ValueGeneratedOnAdd();
                entity.Property(e => e.UserName).HasColumnName("UserName").IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.UserName).HasDatabaseName("UQ__Users__536C85E474AE94DB").IsUnique();
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(256);
                entity.Property(e => e.Salt).IsRequired().HasMaxLength(128);
                entity.Property(e => e.FullName).HasMaxLength(100);
                entity.Property(e => e.Title).HasMaxLength(100).IsUnicode(false);
                entity.Property(e => e.Department).IsRequired().HasMaxLength(255).IsUnicode(false);
                entity.Property(e => e.Picture).HasColumnType("varbinary(max)");
                entity.Property(e => e.isConnected).IsRequired().HasDefaultValue(false);
                entity.Property(e => e.MachineName).HasMaxLength(100);
                entity.Property(e => e.LastActivityTime).HasColumnType("datetime");
                entity.Property(e => e.PresenceStatus).HasDefaultValue(0);
                entity.Property(e => e.CurrentDevice).HasMaxLength(100);
                entity.Property(e => e.IsAvailableForChat).HasDefaultValue(true);
                entity.Property(e => e.IsAdmin).HasDefaultValue(false);
                entity.Property(e => e.IsManager).HasDefaultValue(false);
                entity.Property(e => e.IsHR).HasDefaultValue(false);
                entity.Property(e => e.RefreshToken).HasMaxLength(128);
                entity.Property(e => e.RefreshTokenExpiryTime).HasColumnType("datetime");
                entity.Property(e => e.LastLoginDate).HasColumnType("datetime");
                entity.Property(e => e.LastLoginIp).HasMaxLength(50);
                entity.Property(e => e.FailedLoginAttempts).HasDefaultValue(0);
                entity.Property(e => e.IsLocked).HasDefaultValue(false);
                entity.Property(e => e.LockoutEndTime).HasColumnType("datetime");
                entity.Property(e => e.StatusMessage).HasMaxLength(255);
                entity.Property(e => e.IsActive);
                entity.Property(e => e.CreatedAt).HasColumnType("datetime");
                entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
                entity.HasIndex(e => e.Department).HasDatabaseName("IX_Users_Department");
                entity.HasIndex(e => e.LastActivityTime).HasDatabaseName("IX_Users_LastActivityTime");

                // Configure the one-to-one relationship with AnnualLeaveEntity
                entity.HasOne(u => u.AnnualLeave)
                    .WithOne()
                    .HasForeignKey<AnnualLeaveEntity>(a => a.UserID)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<RequestEntity>(entity =>
            {
                entity.ToTable("Requests");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.RequestUserID).IsRequired();
                entity.Property(e => e.RequestUserFullName).IsRequired().HasMaxLength(255).IsUnicode(false);
                entity.Property(e => e.RequestFromDay).IsRequired().HasColumnType("date");
                entity.Property(e => e.RequestToDay).HasColumnType("date");
                entity.Property(e => e.RequestBeginningTime).HasColumnType("time(7)");
                entity.Property(e => e.RequestEndingTime).HasColumnType("time(7)");
                entity.Property(e => e.RequestReason).HasMaxLength(255);
                entity.Property(e => e.RequestStatus).HasMaxLength(50).HasDefaultValue("Pending");
                entity.Property(e => e.RequestType).IsRequired().HasMaxLength(255).IsUnicode(false);
                entity.Property(e => e.RequestRejectReason).HasMaxLength(255);
                entity.Property(e => e.RequestCloser).HasMaxLength(255).IsUnicode(false);
                entity.Property(e => e.RequestDepartment).IsRequired().HasMaxLength(255).IsUnicode(false);
                entity.Property(e => e.RequestNumberOfDays);
                entity.Property(e => e.RequestHRCloser).HasMaxLength(255);
                entity.Property(e => e.RequestHRStatus).HasMaxLength(50);

                // Configure the relationship with User
                entity.HasOne(r => r.User)
                    .WithMany(u => u.Requests)
                    .HasForeignKey(r => r.RequestUserID)
                    .OnDelete(DeleteBehavior.Restrict);
            });

             builder.Entity<NotificationEntity>(entity =>
             {
                 entity.ToTable("Notifications");
                 entity.HasKey(e => e.NotificationID);
                 entity.Property(e => e.NotificationID).ValueGeneratedOnAdd();
                 entity.Property(e => e.ReceiverID);
                 entity.Property(e => e.SenderID);
                 entity.Property(e => e.MessageID);
                 entity.Property(e => e.IsSeen).IsRequired().HasDefaultValue(false);
                 entity.Property(e => e.Timestamp).IsRequired().HasDefaultValueSql("getdate()").HasColumnType("datetime");
                 entity.Property(e => e.MessageText).HasMaxLength(255).IsUnicode(false);

                 // Define relationships
                 entity.HasOne<User>()
                     .WithMany()
                     .HasForeignKey(n => n.ReceiverID)
                     .OnDelete(DeleteBehavior.Restrict);

                 entity.HasOne<User>()
                     .WithMany()
                     .HasForeignKey(n => n.SenderID)
                     .OnDelete(DeleteBehavior.SetNull)
                     .IsRequired(false);

                 entity.HasOne<MessageEntity>()
                     .WithMany()
                     .HasForeignKey(n => n.MessageID)
                     .OnDelete(DeleteBehavior.SetNull)
                     .IsRequired(false);
             });

            builder.Entity<MessageEntity>(entity =>
            {
                entity.ToTable("Messages");
                entity.HasKey(e => e.MessageID);
                entity.Property(e => e.MessageID).ValueGeneratedOnAdd();
                entity.Property(e => e.SenderID);
                entity.Property(e => e.ReceiverID);
                entity.Property(e => e.MessageText).HasColumnType("nvarchar(max)");
                entity.Property(e => e.Timestamp).IsRequired().HasDefaultValueSql("getdate()").HasColumnType("datetime");
                entity.Property(e => e.IsRead).IsRequired().HasDefaultValue(false);
                entity.Property(e => e.IsDelivered);

                // Define relationships
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(m => m.SenderID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(m => m.ReceiverID)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<AnnualLeaveEntity>(entity =>
            {
                entity.ToTable("AnnualLeave");
                 entity.HasKey(e => e.UserID);
                entity.Property(e => e.UserID).ValueGeneratedNever();
                entity.Property(e => e.FullName).HasMaxLength(100);
                entity.Property(e => e.Annual);
                entity.Property(e => e.CasualLeave);
                entity.Property(e => e.AnnualUsed).HasDefaultValue(0);
                entity.Property(e => e.CasualUsed).HasDefaultValue(0);
                entity.Property(e => e.Permissions);
                entity.Property(e => e.PermissionsUsed);
                entity.Property(e => e.UnpaidUsed).HasDefaultValue(0);

                entity.HasOne<User>()
                    .WithOne(p => p.AnnualLeave)
                    .HasForeignKey<AnnualLeaveEntity>(d => d.UserID)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_AnnualLeave_Users");
            });
        }
    }
}

using System;
using ChangeLogMonitor.Interceptor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ChangeLogMonitor.Interceptor.Migrations
{
    [DbContext(typeof(AuditDbContext))]
    partial class AuditDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("ChangeLogMonitor.Interceptor.Models.AuditLog", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<DateTime>("CreatedAt")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasDefaultValueSql("NOW()")
                        .HasColumnName("created_at");

                    b.Property<byte[]>("Payload")
                        .IsRequired()
                        .HasColumnType("bytea")
                        .HasColumnName("payload");

                    b.Property<DateTime?>("ProcessedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("processed_at");

                    b.Property<string>("TransactionId")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)")
                        .HasColumnName("transaction_id");

                    b.HasKey("Id")
                        .HasName("PK_audit_log");

                    b.HasIndex("CreatedAt")
                        .HasDatabaseName("idx_audit_log_created_at");

                    b.HasIndex("ProcessedAt")
                        .HasDatabaseName("idx_audit_log_processed_at");

                    b.HasIndex("TransactionId")
                        .HasDatabaseName("idx_audit_log_transaction_id");

                    b.ToTable("audit_log", (string)null);
                });
#pragma warning restore 612, 618
        }
    }
}

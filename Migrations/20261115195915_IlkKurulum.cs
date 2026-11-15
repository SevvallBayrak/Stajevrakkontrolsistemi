using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StajEvrak.Migrations
{
    /// <inheritdoc />
    public partial class IlkKurulum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dataset_records",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ad_soyad = table.Column<string>(type: "TEXT", nullable: false),
                    tc_kimlik_no = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                    uyruk = table.Column<string>(type: "TEXT", nullable: true),
                    ogrenci_no = table.Column<string>(type: "TEXT", nullable: false),
                    dogum_tarihi = table.Column<string>(type: "TEXT", nullable: true),
                    ikametgah_adresi = table.Column<string>(type: "TEXT", nullable: true),
                    bolum = table.Column<string>(type: "TEXT", nullable: true),
                    teslim_yili = table.Column<string>(type: "TEXT", nullable: true),
                    onceki_staj_gun = table.Column<int>(type: "INTEGER", nullable: true),
                    eposta = table.Column<string>(type: "TEXT", nullable: true),
                    telefon = table.Column<string>(type: "TEXT", nullable: true),
                    saglik_yardimi = table.Column<string>(type: "TEXT", nullable: true),
                    imported_at = table.Column<string>(type: "TEXT", nullable: true),
                    source_file = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dataset_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    username = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    password_hash = table.Column<string>(type: "TEXT", nullable: false),
                    role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    full_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    student_no = table.Column<string>(type: "TEXT", nullable: true),
                    tc_kimlik_no = table.Column<string>(type: "TEXT", nullable: true),
                    failed_attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    locked_until = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    student_id = table.Column<int>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    form_data = table.Column<string>(type: "TEXT", nullable: false),
                    staj_gunleri = table.Column<string>(type: "TEXT", nullable: true),
                    google_form_url = table.Column<string>(type: "TEXT", nullable: true),
                    admin_note = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    completed_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applications", x => x.id);
                    table.ForeignKey(
                        name: "FK_applications_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "login_attempts",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    identifier = table.Column<string>(type: "TEXT", nullable: false),
                    user_id = table.Column<int>(type: "INTEGER", nullable: true),
                    ip_address = table.Column<string>(type: "TEXT", nullable: true),
                    success = table.Column<bool>(type: "INTEGER", nullable: false),
                    reason = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_login_attempts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    token = table.Column<string>(type: "TEXT", nullable: false),
                    user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    role = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    expires_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    revoked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.token);
                    table.ForeignKey(
                        name: "FK_user_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    application_id = table.Column<int>(type: "INTEGER", nullable: false),
                    original_name = table.Column<string>(type: "TEXT", nullable: false),
                    stored_name = table.Column<string>(type: "TEXT", nullable: false),
                    extension = table.Column<string>(type: "TEXT", nullable: false),
                    size_bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    extracted_data = table.Column<string>(type: "TEXT", nullable: true),
                    report = table.Column<string>(type: "TEXT", nullable: true),
                    is_current = table.Column<bool>(type: "INTEGER", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_documents_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "status_history",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    application_id = table.Column<int>(type: "INTEGER", nullable: false),
                    old_status = table.Column<string>(type: "TEXT", nullable: true),
                    new_status = table.Column<string>(type: "TEXT", nullable: false),
                    changed_by = table.Column<int>(type: "INTEGER", nullable: true),
                    note = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_status_history_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_status_history_users_changed_by",
                        column: x => x.changed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_applications_student_id",
                table: "applications",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_dataset_records_ogrenci_no",
                table: "dataset_records",
                column: "ogrenci_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dataset_records_tc_kimlik_no",
                table: "dataset_records",
                column: "tc_kimlik_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documents_application_id",
                table: "documents",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_login_attempts_identifier",
                table: "login_attempts",
                column: "identifier");

            migrationBuilder.CreateIndex(
                name: "IX_login_attempts_user_id",
                table: "login_attempts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_status_history_application_id",
                table: "status_history",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_status_history_changed_by",
                table: "status_history",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_user_id",
                table: "user_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dataset_records");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "login_attempts");

            migrationBuilder.DropTable(
                name: "status_history");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropTable(
                name: "applications");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}

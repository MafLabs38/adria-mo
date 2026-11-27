using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdriaMo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PairedDevicesAndInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CodeHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CodeExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
                    SecurityStamp = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PairedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    RevokedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InboxPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Photo_OriginalSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Photo_OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Photo_PublishedFileName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Photo_ThumbnailFileName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Photo_Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Photo_Height = table.Column<int>(type: "INTEGER", nullable: false),
                    TakenAt = table.Column<long>(type: "INTEGER", nullable: true),
                    AddedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    AuthorId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InboxPhotos_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pieces_Photo_OriginalSha256",
                table: "Pieces",
                column: "Photo_OriginalSha256");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_CodeHash",
                table: "Devices",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserId",
                table: "Devices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_InboxPhotos_AuthorId",
                table: "InboxPhotos",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_InboxPhotos_Photo_OriginalSha256",
                table: "InboxPhotos",
                column: "Photo_OriginalSha256",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "InboxPhotos");

            migrationBuilder.DropIndex(
                name: "IX_Pieces_Photo_OriginalSha256",
                table: "Pieces");
        }
    }
}

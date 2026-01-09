using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Payment.API.Migrations
{
    /// <inheritdoc />
    public partial class rkJHFJHjvbg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CardNumberEncrypted",
                table: "UserCards");

            migrationBuilder.DropColumn(
                name: "CvvEncrypted",
                table: "UserCards");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CardNumberEncrypted",
                table: "UserCards",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CvvEncrypted",
                table: "UserCards",
                type: "text",
                nullable: true);
        }
    }
}

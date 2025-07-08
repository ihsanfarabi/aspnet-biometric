using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace aspnet_biometric.Migrations
{
    /// <inheritdoc />
    public partial class AddIsBiometricEnabledToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBiometricEnabled",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBiometricEnabled",
                table: "AspNetUsers");
        }
    }
}

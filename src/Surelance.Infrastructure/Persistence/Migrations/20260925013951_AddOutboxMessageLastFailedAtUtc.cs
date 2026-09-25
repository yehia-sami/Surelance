using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Surelance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxMessageLastFailedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastFailedAtUtc",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastFailedAtUtc",
                table: "OutboxMessages");
        }
    }
}

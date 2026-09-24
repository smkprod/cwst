using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Отдельный формат финала: весь турнир можно играть до одной победы, а финал — до двух.
    ///
    /// NULL означает «как везде». Это не то же самое, что записанное в колонку число,
    /// равное BestOf: организатор, который не выбирал ничего особенного, не должен
    /// получить свой финал заморожённым на старом формате, если он потом поменяет
    /// формат турнира целиком. Поэтому совпавшее значение мы храним как NULL.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260924120000_AddTournamentFinalBestOf")]
    public partial class AddTournamentFinalBestOf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FinalBestOf", table: "Tournaments", type: "INTEGER", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "FinalBestOf", table: "Tournaments");
        }
    }
}

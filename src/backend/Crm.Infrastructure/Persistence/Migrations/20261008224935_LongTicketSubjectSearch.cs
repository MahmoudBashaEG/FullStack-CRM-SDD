using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LongTicketSubjectSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION dbo.TicketSubjectContainsLiteral(@subject nvarchar(max), @term nvarchar(max))
                RETURNS bit
                AS
                BEGIN
                    IF @subject IS NULL OR @term IS NULL RETURN 0;
                    DECLARE @length bigint = LEN(@term COLLATE Latin1_General_100_CI_AS_SC);
                    IF @length=0 RETURN 1;
                    DECLARE @last bigint = LEN(@subject COLLATE Latin1_General_100_CI_AS_SC)-@length+1;
                    -- 2,000 SC characters occupy at most 4,000 UTF-16 units: a safe CHARINDEX needle.
                    -- The prefix locates candidates only; the full term decides every match.
                    DECLARE @prefix nvarchar(max) = LEFT(@term COLLATE Latin1_General_100_CI_AS_SC,2000);
                    DECLARE @position bigint = CHARINDEX(@prefix COLLATE Latin1_General_100_CI_AS_SC,@subject COLLATE Latin1_General_100_CI_AS_SC);
                    WHILE @position>0 AND @position<=@last
                    BEGIN
                        IF SUBSTRING(@subject COLLATE Latin1_General_100_CI_AS_SC,@position,@length)=@term COLLATE Latin1_General_100_CI_AS_SC RETURN 1;
                        -- Advance one character, so overlapping candidates are never skipped.
                        SET @position=CHARINDEX(@prefix COLLATE Latin1_General_100_CI_AS_SC,@subject COLLATE Latin1_General_100_CI_AS_SC,@position+1);
                    END;
                    RETURN 0;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION dbo.TicketSubjectContainsLiteral;");
        }
    }
}

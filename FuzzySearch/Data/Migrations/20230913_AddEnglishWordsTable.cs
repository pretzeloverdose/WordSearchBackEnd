using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

public partial class AddEnglishWordsTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Enable extensions
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS fuzzystrmatch;");

        // Create table
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS english_words (
                id SERIAL PRIMARY KEY,
                word VARCHAR(100) NOT NULL UNIQUE,
                length INTEGER NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );
        ");

        // Create indexes
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS idx_english_words_word ON english_words USING gin(word gin_trgm_ops);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS idx_english_words_length ON english_words(length);");

        // Insert sample data
        migrationBuilder.Sql(@"
            INSERT INTO english_words (word, length) VALUES
            ('hello', 5),
            ('world', 5),
            ('fuzzy', 5),
            ('search', 6),
            ('database', 8),
            ('api', 3),
            ('aws', 3),
            ('postgresql', 10),
            ('dictionary', 10)
            ON CONFLICT (word) DO NOTHING;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS english_words;");
        // Optionally, you can drop the extensions if you want:
        // migrationBuilder.Sql("DROP EXTENSION IF EXISTS pg_trgm;");
        // migrationBuilder.Sql("DROP EXTENSION IF EXISTS fuzzystrmatch;");
    }
}
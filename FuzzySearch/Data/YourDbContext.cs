using Microsoft.EntityFrameworkCore;

namespace FuzzySearch.Data
{
    public class YourDbContext : DbContext
    {
        public YourDbContext(DbContextOptions<YourDbContext> options) : base(options) { }

        public DbSet<EnglishWord> EnglishWords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EnglishWord>(entity =>
            {
                entity.ToTable("english_words");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Word)
                      .HasDatabaseName("idx_english_words_word");
                // The following lines are not supported by EF Core out of the box:
                // .HasMethod("gin")
                // .HasOperators("gin_trgm_ops");
                // If you need to use a GIN index with trigrams, you must do it via a raw SQL migration.
                entity.HasIndex(e => e.Length)
                      .HasDatabaseName("idx_english_words_length");
                entity.Property(e => e.Word).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Length).IsRequired();
                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
        }
    }

    public class EnglishWord
    {
        public int Id { get; set; }
        public string Word { get; set; }
        public int Length { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

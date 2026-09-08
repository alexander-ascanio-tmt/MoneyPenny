using Microsoft.EntityFrameworkCore;
using MoneyPenny.Models.Rag;
using Pgvector.EntityFrameworkCore;

namespace MoneyPenny.Data;

public class VectorDbContext : DbContext
{
    public const int EmbeddingDimensions = 1536;

    public VectorDbContext(DbContextOptions<VectorDbContext> options)
        : base(options)
    {
    }

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<TicketEmbedding> TicketEmbeddings => Set<TicketEmbedding>();
    public DbSet<RagQueryLog> RagQueryLogs => Set<RagQueryLog>();
    public DbSet<CommentImageTextCache> CommentImageTextCaches => Set<CommentImageTextCache>();
    public DbSet<TicketIntent> TicketIntents => Set<TicketIntent>();
    public DbSet<TicketIntentAssignment> TicketIntentAssignments => Set<TicketIntentAssignment>();
    public DbSet<TicketCommentSignals> TicketCommentSignals => Set<TicketCommentSignals>();
    public DbSet<TicketProcessedComment> TicketProcessedComments => Set<TicketProcessedComment>();
    public DbSet<TicketUrgency> TicketUrgencies => Set<TicketUrgency>();
    public DbSet<UrgencyRule> UrgencyRules => Set<UrgencyRule>();
    public DbSet<UrgencyDetectionConfig> UrgencyDetectionConfigs => Set<UrgencyDetectionConfig>();
    public DbSet<IntentDetectionConfig> IntentDetectionConfigs => Set<IntentDetectionConfig>();
    public DbSet<RagPromptTemplate> RagPromptTemplates => Set<RagPromptTemplate>();
    public DbSet<RagPromptSelectionRule> RagPromptSelectionRules => Set<RagPromptSelectionRule>();
    public DbSet<UrgencyProfile> UrgencyProfiles => Set<UrgencyProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.ToTable("document_chunks");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.TicketNumber).HasMaxLength(50);
            entity.Property(c => c.Source)
                .HasConversion<int>()
                .HasDefaultValue(DocumentChunkSource.TicketDocument);
            entity.Property(c => c.IsKnowledgeBase)
                .HasDefaultValue(false);
            entity.HasIndex(c => c.TicketId);
            entity.HasIndex(c => c.Source);
            entity.HasIndex(c => new { c.TicketId, c.Source });
        });

        modelBuilder.Entity<TicketEmbedding>(entity =>
        {
            entity.ToTable("ticket_embeddings");
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.DocumentChunk)
                .WithMany()
                .HasForeignKey(e => e.DocumentChunkId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.TicketId);
            entity.Property(e => e.Embedding)
                .HasColumnName("Vector")
                .HasColumnType($"vector({EmbeddingDimensions})");
        });

        modelBuilder.Entity<RagQueryLog>(entity =>
        {
            entity.ToTable("rag_query_logs");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.UserId).HasMaxLength(450);
            entity.Property(l => l.RatedByUserId).HasMaxLength(450);
            entity.Property(l => l.TeamSupportActionId).HasMaxLength(50);
            entity.Property(l => l.ResponseType)
                .HasConversion<int>()
                .HasDefaultValue(RagResponseType.Gpt);
            entity.HasIndex(l => new { l.TicketId, l.ResponseType });
        });

        modelBuilder.Entity<CommentImageTextCache>(entity =>
        {
            entity.ToTable("comment_image_text_cache");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.ImageSource).HasMaxLength(2048);
            entity.Property(c => c.VisionModel).HasMaxLength(100);
            entity.HasIndex(c => c.ImageSource).IsUnique();
            entity.HasIndex(c => c.TicketId);
            entity.HasIndex(c => c.TicketActionId);
        });

        modelBuilder.Entity<TicketIntent>(entity =>
        {
            entity.ToTable("ticket_intents");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Code).HasMaxLength(50).IsRequired();
            entity.Property(i => i.Name).HasMaxLength(100).IsRequired();
            entity.Property(i => i.Description).HasMaxLength(500);
            entity.Property(i => i.IsActive).HasDefaultValue(true);
            entity.HasIndex(i => i.Code).IsUnique();
            entity.HasIndex(i => i.SortOrder);
        });

        modelBuilder.Entity<TicketIntentAssignment>(entity =>
        {
            entity.ToTable("ticket_intent_assignments");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.TicketNumber).HasMaxLength(50);
            entity.Property(a => a.ClassifierVersion).HasMaxLength(100);
            entity.HasOne(a => a.Intent)
                .WithMany(i => i.Assignments)
                .HasForeignKey(a => a.IntentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(a => a.TicketId);
            entity.HasIndex(a => a.IntentId);
            entity.HasIndex(a => new { a.TicketId, a.IntentId }).IsUnique();
        });

        modelBuilder.Entity<TicketCommentSignals>(entity =>
        {
            entity.ToTable("ticket_comment_signals");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.TicketNumber).HasMaxLength(50);
            entity.Property(s => s.HasMessageBox).HasDefaultValue(false);
            entity.Property(s => s.HasAttachment).HasDefaultValue(false);
            entity.Property(s => s.MessageBoxDetail).HasMaxLength(4000);
            entity.Property(s => s.AttachmentDetail).HasMaxLength(500);
            entity.Property(s => s.Source).HasMaxLength(100);
            entity.HasIndex(s => s.TicketId).IsUnique();
        });

        modelBuilder.Entity<TicketProcessedComment>(entity =>
        {
            entity.ToTable("ticket_processed_comments");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.TicketNumber).HasMaxLength(50);
            entity.Property(c => c.ProcessedText).IsRequired();
            entity.Property(c => c.ImageExtractionWarning).HasMaxLength(1000);
            entity.HasIndex(c => c.TicketId).IsUnique();
        });

        modelBuilder.Entity<TicketUrgency>(entity =>
        {
            entity.ToTable("ticket_urgency");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.TicketNumber).HasMaxLength(50);
            entity.Property(u => u.IsUrgent).HasDefaultValue(false);
            entity.Property(u => u.Reason).HasMaxLength(500);
            entity.Property(u => u.ClassifierVersion).HasMaxLength(100);
            entity.HasIndex(u => u.TicketId).IsUnique();
        });

        modelBuilder.Entity<UrgencyRule>(entity =>
        {
            entity.ToTable("urgency_rules");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Phrase).HasMaxLength(200).IsRequired();
            entity.Property(r => r.IsActive).HasDefaultValue(true);
            entity.HasIndex(r => r.SortOrder);
        });

        modelBuilder.Entity<UrgencyDetectionConfig>(entity =>
        {
            entity.ToTable("urgency_detection_config");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<IntentDetectionConfig>(entity =>
        {
            entity.ToTable("intent_detection_config");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<RagPromptTemplate>(entity =>
        {
            entity.ToTable("rag_prompt_templates");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Code).HasMaxLength(50).IsRequired();
            entity.Property(t => t.Name).HasMaxLength(100).IsRequired();
            entity.Property(t => t.IsActive).HasDefaultValue(true);
            entity.HasIndex(t => t.Code).IsUnique();
            entity.HasIndex(t => t.SortOrder);
        });

        modelBuilder.Entity<RagPromptSelectionRule>(entity =>
        {
            entity.ToTable("rag_prompt_selection_rules");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).HasMaxLength(100);
            entity.Property(r => r.IntentCode).HasMaxLength(50);
            entity.Property(r => r.IsActive).HasDefaultValue(true);
            entity.HasOne(r => r.PromptTemplate)
                .WithMany(t => t.SelectionRules)
                .HasForeignKey(r => r.PromptTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(r => r.Priority);
        });

        modelBuilder.Entity<UrgencyProfile>(entity =>
        {
            entity.ToTable("urgency_profiles");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Code).HasMaxLength(20).IsRequired();
            entity.Property(p => p.Name).HasMaxLength(50).IsRequired();
            entity.HasIndex(p => p.Code).IsUnique();
        });
    }
}

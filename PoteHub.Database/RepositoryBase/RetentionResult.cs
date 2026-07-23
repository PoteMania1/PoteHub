namespace PoteHub.Database.RepositoryBase;

public class RetentionResult
{
    public bool DailyMaintenanceRan { get; set; }

    public int SummarizedDays { get; set; }

    public int DeletedClanChanges { get; set; }

    public int DeletedMemberChanges { get; set; }

    public int DeletedSyncRuns { get; set; }

    public int FinalizedSeasons { get; set; }
}
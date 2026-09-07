using Microsoft.EntityFrameworkCore;
using MySql.EntityFrameworkCore.Extensions;
using SysPulse.Data.Entities;

namespace SysPulse.Data.Context;

public class SysPulseDbContext : DbContext{
    public DbSet<SystemSnapshot> Snapshots => Set<SystemSnapshot>();

    public SysPulseDbContext(DbContextOptions<SysPulseDbContext> options) : base(options){
    }

    public SysPulseDbContext(){
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder){
        if(!optionsBuilder.IsConfigured){
            string localDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SysPulse");
            Directory.CreateDirectory(localDir);
            string dbPath=Path.Combine(localDir, "syspulse.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder){
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<SystemSnapshot>(entity =>{
            entity.HasIndex(e => e.TimestampUtc);
            entity.HasIndex(e => e.Hostname);
        });
    }
}

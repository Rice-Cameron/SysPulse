using System.ComponentModel.DataAnnotations;

namespace SysPulse.Data.Entities;

public class SystemSnapshot{
    [Key]
    public int Id{get;set;}
    public DateTime TimestampUtc{get;set;}=DateTime.UtcNow;
    public double CpuUsagePercent{get;set;}
    public double MemoryUsagePercent{get;set;}
    public double MemoryUsedGb{get;set;}
    public double MemoryTotalGb{get;set;}
    public double SwapUsagePercent{get;set;}
    public double DiskUsagePercent{get;set;}
    public double NetworkDownloadKbps{get;set;}
    public double NetworkUploadKbps{get;set;}
    [MaxLength(256)]
    public string? Note{get;set;}
}

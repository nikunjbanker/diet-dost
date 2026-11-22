/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Nutrition.Application.Common.Options;
using Nutrition.WebGateway.Initialization.Abstractions;

namespace Nutrition.WebGateway.Initialization.Seeders;

/// <summary>
/// Ordered seeder mirroring static media assets (sample meals and baseline progress photos) from the container's
/// local wwwroot to the persistent storage volume (e.g. Azure Files SMB share at /app/data/wwwroot/uploads).
/// Prevents HTTP 404 errors for seed media in production cloud environments.
/// </summary>
public sealed class StorageMediaMirrorSeeder : IDatabaseSeeder
{
    private static readonly string[] SeedMediaSubdirectories = ["meals", "progress"];
    private readonly IWebHostEnvironment? _hostEnv;
    private readonly StorageOptions _storageOptions;
    private readonly ILogger<StorageMediaMirrorSeeder> _logger;

    public int Order => 30;

    public StorageMediaMirrorSeeder(
        IServiceProvider services,
        IOptions<StorageOptions> storageOptions,
        ILogger<StorageMediaMirrorSeeder> logger)
    {
        _hostEnv = services.GetService<IWebHostEnvironment>();
        _storageOptions = storageOptions?.Value ?? new StorageOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_hostEnv == null || string.IsNullOrWhiteSpace(_storageOptions.WebRootPath))
            {
                return Task.CompletedTask;
            }

            var sourceUploads = Path.Combine(_hostEnv.WebRootPath, "uploads");
            var destUploads = Path.Combine(_storageOptions.WebRootPath, "uploads");

            if (!Directory.Exists(sourceUploads))
            {
                return Task.CompletedTask;
            }

            if (string.Equals(Path.GetFullPath(sourceUploads), Path.GetFullPath(destUploads), StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            foreach (var subDir in SeedMediaSubdirectories)
            {
                var srcDir = Path.Combine(sourceUploads, subDir);
                var dstDir = Path.Combine(destUploads, subDir);

                if (!Directory.Exists(srcDir)) continue;
                Directory.CreateDirectory(dstDir);

                foreach (var file in Directory.GetFiles(srcDir))
                {
                    var destFile = Path.Combine(dstDir, Path.GetFileName(file));
                    if (!File.Exists(destFile))
                    {
                        File.Copy(file, destFile, overwrite: false);
                        if (_logger.IsEnabled(LogLevel.Information))
                        {
                            var fileName = Path.GetFileName(file);
                            _logger.LogInformation("[STORAGE] Mirrored seed media asset '{FileName}' to persistent storage volume '{DestPath}'.", fileName, destFile);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[STORAGE] Non-fatal warning while mirroring seed media assets to persistent volume.");
        }

        return Task.CompletedTask;
    }
}

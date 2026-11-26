using SmartLab.BLL.DTOs.Settings;

namespace SmartLab.BLL.Interfaces.Settings;

public interface ISystemSettingService
{
    /// <summary>Every setting Admin can change, including ones not set yet (Value null).</summary>
    Task<IReadOnlyList<SystemSettingResponse>> GetSettingsAsync(CancellationToken ct = default);

    /// <summary>Checks the value for the key, then creates or updates the row.</summary>
    Task<SystemSettingResponse> UpdateSettingAsync(string key, UpdateSystemSettingRequest request, CancellationToken ct = default);
}

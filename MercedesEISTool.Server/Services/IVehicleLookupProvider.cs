using MercedesEISTool.Contracts.Models;

namespace MercedesEISTool.Server.Services;

public interface IVehicleLookupProvider
{
    Task<VehicleInfoDto> LookupAsync(string registration, CancellationToken cancellationToken = default);
}

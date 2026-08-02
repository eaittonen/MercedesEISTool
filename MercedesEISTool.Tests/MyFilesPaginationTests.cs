using System.Reflection;
using MercedesEISTool.ApiClient;
using MercedesEISTool.Contracts.Models;
using MercedesEISTool.GUI.ViewModels;

namespace MercedesEISTool.Tests;

public class MyFilesPaginationTests
{
    [Fact]
    public async Task RefreshUploadedFilesAsync_UsesServerPaginationMetadata()
    {
        var apiClient = new FakeApiClient(new StoredFileListResponse
        {
            Items =
            [
                new StoredFileListItemDto { Id = Guid.NewGuid(), OriginalFileName = "one.bin" },
                new StoredFileListItemDto { Id = Guid.NewGuid(), OriginalFileName = "two.bin" }
            ],
            Page = 2,
            PageSize = 10,
            TotalCount = 25,
            TotalPages = 3
        });

        var viewModel = new MainViewModel(apiClient);
        viewModel.MyFilesCurrentPage = 2;
        viewModel.MyFilesPageSize = 10;

        var refreshMethod = typeof(MainViewModel).GetMethod("RefreshUploadedFilesAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(refreshMethod);

        await (Task)refreshMethod!.Invoke(viewModel, null)!;

        Assert.Equal(2, viewModel.MyFilesCurrentPage);
        Assert.Equal(10, viewModel.MyFilesPageSize);
        Assert.Equal(25, viewModel.MyFilesTotalCount);
        Assert.Equal(3, viewModel.MyFilesTotalPages);
        Assert.True(viewModel.MyFilesHasPreviousPage);
        Assert.True(viewModel.MyFilesHasNextPage);
        Assert.Contains("Page 2 of 3", viewModel.MyFilesPageStatus);
    }

    [Fact]
    public async Task SearchMyFiles_ResetsToFirstPageBeforeRefreshing()
    {
        var apiClient = new FakeApiClient(new StoredFileListResponse
        {
            Items = [new StoredFileListItemDto { Id = Guid.NewGuid(), OriginalFileName = "result.bin" }],
            Page = 1,
            PageSize = 50,
            TotalCount = 1,
            TotalPages = 1
        });

        var viewModel = new MainViewModel(apiClient);
        viewModel.MyFilesCurrentPage = 3;
        viewModel.MyFilesSearchText = "abc";

        var searchMethod = typeof(MainViewModel).GetMethod("SearchMyFiles", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(searchMethod);

        await (Task)searchMethod!.Invoke(viewModel, null)!;

        Assert.Equal(1, viewModel.MyFilesCurrentPage);
        Assert.Equal("abc", apiClient.LastSearch);
        Assert.Single(apiClient.Requests);
        Assert.Equal(1, apiClient.Requests[0].Page);
        Assert.Equal(50, apiClient.Requests[0].PageSize);
    }

    [Fact]
    public void PopulateWorkspaceFromDetails_RestoresMetadataAndSharedBytes()
    {
        var viewModel = new MainViewModel(new FakeApiClient(new StoredFileListResponse()));
        var bytes = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        var details = new StoredFileDetailsDto
        {
            Id = Guid.NewGuid(),
            OriginalFileName = "restored.bin",
            UserProvidedVin = "VIN12345678901234",
            DetectedVin = "DETECTEDVIN",
            RegistrationNumber = "ABC-123",
            CustomerName = "Ada Lovelace",
            AdditionalInformation = "Customer note",
            DetectedFormat = "CGDI MB",
            EisType = "EIS-A",
            McuType = "MCU-A",
            KeyCount = 4,
            EisPassword = "1234",
            Ssid = "SSID-1",
            FileSizeBytes = bytes.Length,
            Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))
        };

        var populateMethod = typeof(MainViewModel).GetMethod("PopulateWorkspaceFromDetails", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(populateMethod);

        populateMethod!.Invoke(viewModel, new object?[] { details, bytes });

        Assert.Equal("restored.bin", viewModel.SelectedFileName);
        Assert.Equal("VIN12345678901234", viewModel.VehicleIdentifier);
        Assert.Equal("ABC-123", viewModel.RegistrationNumber);
        Assert.Equal("Ada Lovelace", viewModel.CustomerName);
        Assert.Equal("Customer note", viewModel.AdditionalInformation);
        Assert.Equal(bytes, viewModel.SelectedFileBytes);
        Assert.Contains("0000", viewModel.RawHexEditorText);
    }

    [Fact]
    public void PopulateWorkspaceFromDetails_DoesNotAutoConfirmDetectedVinWithoutUserInput()
    {
        var viewModel = new MainViewModel(new FakeApiClient(new StoredFileListResponse()));
        var details = new StoredFileDetailsDto
        {
            Id = Guid.NewGuid(),
            OriginalFileName = "detected-only.bin",
            DetectedVin = "VIN12345678901234",
            RegistrationNumber = string.Empty,
            CustomerName = "Ada Lovelace"
        };

        var populateMethod = typeof(MainViewModel).GetMethod("PopulateWorkspaceFromDetails", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(populateMethod);

        populateMethod!.Invoke(viewModel, new object?[] { details, null });

        Assert.Equal("VIN12345678901234", viewModel.VehicleIdentifier);
        Assert.False(viewModel.VinConfirmedByUser);
        Assert.False(viewModel.CanUpload);
    }

    [Fact]
    public void ConvertDump_EnablesSavingAfterTheSelectedTargetFormatIsApplied()
    {
        var viewModel = new MainViewModel(new FakeApiClient(new StoredFileListResponse()));
        viewModel.SelectedFileName = "sample.bin";
        viewModel.SelectedFileBytes = new byte[] { 0x00, 0x01, 0x02 };
        viewModel.DetectedFormat = "CGDI MB";
        viewModel.SelectedTargetFormat = "VVDI MB Tool";
        viewModel.CanSave = false;

        var convertMethod = typeof(MainViewModel).GetMethod("ConvertDump", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(convertMethod);

        convertMethod!.Invoke(viewModel, null);

        Assert.Equal("VVDI MB Tool", viewModel.DetectedFormat);
        Assert.True(viewModel.CanSave);
        Assert.Contains("converted", viewModel.Status, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeApiClient : IMercedesEisApiClient
    {
        private readonly StoredFileListResponse _storedFileResponse;

        public FakeApiClient(StoredFileListResponse storedFileResponse)
        {
            _storedFileResponse = storedFileResponse;
        }

        public List<(string? Search, int Page, int PageSize)> Requests { get; } = new();
        public string? LastSearch { get; private set; }

        public Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new HealthResponse { IsHealthy = true, Status = "ok" });

        public Task<AuthResponseDto> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
            => Task.FromResult(new AuthResponseDto());

        public Task<CurrentUserResponseDto> GetCurrentUserAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new CurrentUserResponseDto());

        public Task<AdminUserListResponseDto> GetAdminUsersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminUserListResponseDto());

        public Task<AdminUserActionResponseDto> DisableUserAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminUserActionResponseDto());

        public Task<AdminUserActionResponseDto> EnableUserAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminUserActionResponseDto());

        public Task<OrganizationListResponseDto> GetOrganizationsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new OrganizationListResponseDto());

        public Task<List<OrganizationOptionDto>> GetOrganizationOptionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<OrganizationOptionDto>());

        public Task<List<RoleOptionDto>> GetRoleOptionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<RoleOptionDto>());

        public Task<OrganizationDetailDto> CreateOrganizationAsync(CreateOrganizationRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new OrganizationDetailDto());

        public Task<OrganizationDetailDto> UpdateOrganizationAsync(string organizationId, UpdateOrganizationRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new OrganizationDetailDto());

        public Task<AdminUserActionResponseDto> DeleteOrganizationAsync(string organizationId, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminUserActionResponseDto());

        public Task<AdminUserActionResponseDto> CreateUserAsync(CreateOrUpdateUserRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminUserActionResponseDto());

        public Task<AdminUserActionResponseDto> UpdateUserAsync(string userId, CreateOrUpdateUserRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminUserActionResponseDto());

        public Task<AdminUserActionResponseDto> DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminUserActionResponseDto());

        public Task<AdminUserActionResponseDto> ResetUserPasswordAsync(string userId, ResetPasswordRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminUserActionResponseDto());

        public Task<AdminUserActionResponseDto> SetUserPasswordChangeRequirementAsync(string userId, ForcePasswordChangeRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminUserActionResponseDto());

        public Task<StorageDiagnosticsResponseDto> GetStorageDiagnosticsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new StorageDiagnosticsResponseDto());

        public void SetAccessToken(string? accessToken) { }

        public Task<AnalyzeDumpResponse> AnalyzeDumpAsync(byte[] data, string fileName, CancellationToken cancellationToken = default)
            => Task.FromResult(new AnalyzeDumpResponse());

        public Task<UploadDumpResponse> UploadDumpAsync(byte[] data, string fileName, string? userProvidedVin, string? userProvidedRegistrationNumber, bool vehicleIdentifierConfirmed, string? customerName, CancellationToken cancellationToken = default)
            => Task.FromResult(new UploadDumpResponse());

        public Task<UploadedDumpListResponse> GetUploadedDumpsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new UploadedDumpListResponse());

        public Task<StoredFileListResponse> GetStoredFilesAsync(string? search = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        {
            Requests.Add((search, page, pageSize));
            LastSearch = search;
            return Task.FromResult(_storedFileResponse);
        }

        public Task<StoredFileDetailsDto> GetStoredFileDetailsAsync(Guid storedFileId, CancellationToken cancellationToken = default)
            => Task.FromResult(new StoredFileDetailsDto());

        public Task<byte[]> DownloadStoredFileAsync(Guid storedFileId, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task<StoredFileDetailsDto> ReanalyzeStoredFileAsync(Guid storedFileId, CancellationToken cancellationToken = default)
            => Task.FromResult(new StoredFileDetailsDto());

        public Task<CompareDumpsResponse> CompareDumpsAsync(byte[] left, byte[] right, string leftFileName, string rightFileName, string vehicleIdentifier, string registrationNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(new CompareDumpsResponse());

        public Task<VehicleInfoDto> LookupVehicleAsync(string registration, CancellationToken cancellationToken = default)
            => Task.FromResult(new VehicleInfoDto { Registration = registration });

        public Task<string> LookupVehicleRawAsync(string registration, CancellationToken cancellationToken = default)
            => Task.FromResult($"{{\"registration\":\"{registration}\"}}");

        public Task<BulkConsumePreviewResponse> PreviewBulkConsumeAsync(string sourceFolderPath, bool includeSubdirectories, CancellationToken cancellationToken = default)
            => Task.FromResult(new BulkConsumePreviewResponse());

        public Task<BulkConsumeImportResponse> ImportBulkConsumeAsync(BulkConsumeImportRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new BulkConsumeImportResponse());

        public Task<HttpResponseMessage> UploadBulkConsumeFileAsync(MultipartFormDataContent content, CancellationToken cancellationToken = default)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }
}

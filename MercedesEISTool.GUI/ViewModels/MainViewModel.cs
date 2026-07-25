using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using MercedesEISTool.ApiClient;
using MercedesEISTool.Contracts.Models;
using MercedesEISTool.GUI.Configuration;
using MercedesEISTool.GUI.Services;

namespace MercedesEISTool.GUI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private IMercedesEisApiClient _apiClient;
    private readonly IClipboardService _clipboardService = new AvaloniaClipboardService();

    [ObservableProperty]
    private string _vin = "Unknown";

    [ObservableProperty]
    private string _eisType = "Unknown";

    [ObservableProperty]
    private string _detectedFormat = "Unknown";

    [ObservableProperty]
    private string _mcu = "Unknown";

    [ObservableProperty]
    private string _keyCount = "0";

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private string _serverStatus = "Connecting";

    [ObservableProperty]
    private string _currentUserDisplay = "Not signed in";

    [ObservableProperty]
    private bool _isAdministrator;

    [ObservableProperty]
    private ObservableCollection<AdminUserListItemDto> _adminUsers = new();

    [ObservableProperty]
    private ObservableCollection<OrganizationSummaryDto> _organizations = new();

    [ObservableProperty]
    private ObservableCollection<OrganizationOptionDto> _organizationOptions = new();

    [ObservableProperty]
    private ObservableCollection<RoleOptionDto> _roleOptions = new();

    [ObservableProperty]
    private OrganizationSummaryDto? _selectedOrganization;

    [ObservableProperty]
    private OrganizationOptionDto? _selectedOrganizationOption;

    [ObservableProperty]
    private AdminUserListItemDto? _selectedAdminUser;

    [ObservableProperty]
    private string _adminStatus = string.Empty;

    [ObservableProperty]
    private string _organizationName = string.Empty;

    [ObservableProperty]
    private string _organizationContactEmail = string.Empty;

    [ObservableProperty]
    private string _organizationCountry = string.Empty;

    [ObservableProperty]
    private bool _organizationIsActive = true;

    [ObservableProperty]
    private string _organizationLicenseType = "Standard";

    [ObservableProperty]
    private string _organizationMaxUsers = "4";

    [ObservableProperty]
    private string _organizationLicenseExpiration = string.Empty;

    [ObservableProperty]
    private string _adminUserEmail = string.Empty;

    [ObservableProperty]
    private string _adminUserDisplayName = string.Empty;

    [ObservableProperty]
    private string _adminUserPassword = string.Empty;

    [ObservableProperty]
    private string _adminUserOrganizationId = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _selectedAdminUserRoles = new();

    [ObservableProperty]
    private bool _adminUserIsEnabled = true;

    [ObservableProperty]
    private bool _adminUserForcePasswordChange;

    [ObservableProperty]
    private bool _isAdminFormEditing;

    [ObservableProperty]
    private bool _isCreatingOrganization;

    [ObservableProperty]
    private bool _isCreatingUser;

    [ObservableProperty]
    private bool _isSubmittingAdminAction;

    [ObservableProperty]
    private string _selectedFileName = "No file selected";

    [ObservableProperty]
    private string _selectedFilePath = string.Empty;

    [ObservableProperty]
    private string _analysisSummary = string.Empty;

    [ObservableProperty]
    private string _uploadSummary = string.Empty;

    [ObservableProperty]
    private string _uploadedFilesSummary = string.Empty;

    [ObservableProperty]
    private ObservableCollection<StoredFileListItemViewModel> _storedFiles = new();

    private List<StoredFileListItemViewModel> _allStoredFiles = new();

    [ObservableProperty]
    private StoredFileListItemViewModel? _selectedStoredFile;

    [ObservableProperty]
    private string _selectedStoredFileDisplay = "Selected: No file selected";

    [ObservableProperty]
    private string _myFilesSearchText = string.Empty;

    [ObservableProperty]
    private string _myFilesDetails = string.Empty;

    [ObservableProperty]
    private string _customerName = string.Empty;

    [ObservableProperty]
    private Guid? _selectedStoredFileId;

    [ObservableProperty]
    private Guid? _currentWorkspaceStoredFileId;

    [ObservableProperty]
    private bool _isLoadingStoredFile;

    [ObservableProperty]
    private string _vinStatus = "NotMapped";

    [ObservableProperty]
    private string _eisPassword = "Not mapped";

    [ObservableProperty]
    private string _ssid = "Not mapped";

    [ObservableProperty]
    private bool? _initialized;

    [ObservableProperty]
    private bool? _personalized;

    [ObservableProperty]
    private bool? _tpCleared;

    [ObservableProperty]
    private bool? _activated;

    [ObservableProperty]
    private bool? _dealerEis;

    [ObservableProperty]
    private bool? _fbs4;

    [ObservableProperty]
    private ObservableCollection<KeySlotDto> _keySlots = new();

    [ObservableProperty]
    private byte[]? _selectedFileBytes;

    [ObservableProperty]
    private long _selectedFileSize;

    [ObservableProperty]
    private string _selectedFileSha256 = string.Empty;

    [ObservableProperty]
    private int _selectedMainTabIndex;

    [ObservableProperty]
    private string _apiBaseUrl = string.Empty;

    [ObservableProperty]
    private string _selectedTargetFormat = "CGDI MB";

    [ObservableProperty]
    private string _vehicleIdentifier = string.Empty;

    [ObservableProperty]
    private string _registrationNumber = string.Empty;

    [ObservableProperty]
    private bool _vinConfirmedByUser;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _canUpload;

    [ObservableProperty]
    private string _lastChecked = string.Empty;

    [ObservableProperty]
    private string _connectionReason = string.Empty;

    [ObservableProperty]
    private string _connectionUrl = string.Empty;

    [ObservableProperty]
    private string _rawHexText = string.Empty;

    [ObservableProperty]
    private string _compareAPath = string.Empty;

    [ObservableProperty]
    private string _compareBPath = string.Empty;

    [ObservableProperty]
    private string _compareText = string.Empty;

    [ObservableProperty]
    private string _compareSummary = string.Empty;

    [ObservableProperty]
    private string _compareOffsets = string.Empty;

    private byte[]? _compareABytes;

    private byte[]? _compareBBytes;

    private ResearchFolderAnalysis? _lastResearchAnalysis;
    private List<ResearchMatchRecord> _lastResearchMatches = new();

    [ObservableProperty]
    private string _sequenceStartOffset = string.Empty;

    [ObservableProperty]
    private string _sequenceLength = "1";

    [ObservableProperty]
    private string _sequenceSearchText = string.Empty;

    [ObservableProperty]
    private string _sequenceResults = string.Empty;

    [ObservableProperty]
    private string _researchEisPath = string.Empty;

    [ObservableProperty]
    private string _researchKeyPath = string.Empty;

    [ObservableProperty]
    private string _researchComparePath = string.Empty;

    [ObservableProperty]
    private string _researchFolderPath = string.Empty;

    [ObservableProperty]
    private string _researchStartOffset = string.Empty;

    [ObservableProperty]
    private string _researchLength = "8";

    [ObservableProperty]
    private string _researchSelectedSourceFile = "EIS dump";

    [ObservableProperty]
    private string _researchSelectedMode = "Exact";

    [ObservableProperty]
    private string _researchXorValue = "0";

    [ObservableProperty]
    private string _researchSearchResults = string.Empty;

    [ObservableProperty]
    private string _researchAnnotationName = string.Empty;

    [ObservableProperty]
    private string _researchAnnotationFileFormat = string.Empty;

    [ObservableProperty]
    private string _researchAnnotationOffset = string.Empty;

    [ObservableProperty]
    private string _researchAnnotationLength = string.Empty;

    [ObservableProperty]
    private string _researchAnnotationByteOrder = string.Empty;

    [ObservableProperty]
    private string _researchAnnotationNotes = string.Empty;

    [ObservableProperty]
    private string _researchSelectedConfidence = "Unknown";

    [ObservableProperty]
    private string _researchAnnotationsText = string.Empty;

    [ObservableProperty]
    private string _researchFolderAnalysisText = string.Empty;

    [ObservableProperty]
    private string _researchReportPath = string.Empty;

    [ObservableProperty]
    private bool _canConvert = false;

    [ObservableProperty]
    private bool _canSave = false;

    public MainViewModel(IMercedesEisApiClient? apiClient = null, IConfiguration? configuration = null)
    {
        ApiBaseUrl = "https://tool.mestariverkko.fi";
        _apiClient = apiClient ?? CreateApiClient(ApiBaseUrl);
        ConnectionUrl = ApiBaseUrl;
        _ = RefreshServerStatusAsync();
        _ = LoadCurrentUser();
    }

    partial void OnApiBaseUrlChanged(string value)
    {
        _apiClient = CreateApiClient(value);
        ConnectionUrl = value;
        _ = RefreshServerStatusAsync();
    }

    partial void OnVehicleIdentifierChanged(string value)
    {
        UpdateUploadAvailability();
    }

    partial void OnRegistrationNumberChanged(string value)
    {
        UpdateUploadAvailability();
    }

    partial void OnVinConfirmedByUserChanged(bool value)
    {
        UpdateUploadAvailability();
    }

    partial void OnServerStatusChanged(string value)
    {
        UpdateUploadAvailability();
        UpdateStoredFileCommandStates();
    }

    partial void OnSelectedFileNameChanged(string value)
    {
        UpdateUploadAvailability();
    }

    partial void OnIsBusyChanged(bool value)
    {
        UpdateUploadAvailability();
        UpdateStoredFileCommandStates();
    }

    partial void OnSelectedStoredFileChanged(StoredFileListItemViewModel? value)
    {
        SelectedStoredFileId = value?.Id;
        SelectedStoredFileDisplay = value is null ? "Selected: No file selected" : $"Selected: {value.OriginalFileName}";
        MyFilesDetails = value is null ? string.Empty : $"Selected row: {value.OriginalFileName}";
        UpdateStoredFileCommandStates();
    }

    partial void OnIsLoadingStoredFileChanged(bool value)
    {
        UpdateStoredFileCommandStates();
    }

    partial void OnMyFilesSearchTextChanged(string value)
    {
        ApplyStoredFilesFilter();
    }

    partial void OnSelectedOrganizationChanged(OrganizationSummaryDto? value)
    {
        if (value is null)
        {
            OrganizationName = string.Empty;
            OrganizationContactEmail = string.Empty;
            OrganizationCountry = string.Empty;
            OrganizationIsActive = true;
            OrganizationLicenseType = "Standard";
            OrganizationMaxUsers = "4";
            OrganizationLicenseExpiration = string.Empty;
            return;
        }

        OrganizationName = value.Name;
        OrganizationContactEmail = value.ContactEmail;
        OrganizationCountry = value.Country;
        OrganizationIsActive = value.IsActive;
        OrganizationLicenseType = value.LicenseType;
        OrganizationMaxUsers = value.MaxUsers.ToString();
        OrganizationLicenseExpiration = value.LicenseExpirationUtc?.ToString("yyyy-MM-dd") ?? string.Empty;
    }

    partial void OnSelectedOrganizationOptionChanged(OrganizationOptionDto? value)
    {
        if (value is not null)
        {
            AdminUserOrganizationId = value.Id;
        }
    }

    partial void OnSelectedAdminUserChanged(AdminUserListItemDto? value)
    {
        if (value is null)
        {
            AdminUserEmail = string.Empty;
            AdminUserDisplayName = string.Empty;
            AdminUserPassword = string.Empty;
            AdminUserOrganizationId = string.Empty;
            SelectedAdminUserRoles = new ObservableCollection<string>();
            AdminUserIsEnabled = true;
            AdminUserForcePasswordChange = false;
            IsAdminFormEditing = false;
            IsCreatingUser = false;
            return;
        }

        AdminUserEmail = value.Email;
        AdminUserDisplayName = value.DisplayName;
        AdminUserPassword = string.Empty;
        AdminUserOrganizationId = value.OrganizationId ?? string.Empty;
        SelectedAdminUserRoles = new ObservableCollection<string>(value.Roles ?? []);
        AdminUserIsEnabled = value.IsEnabled;
        AdminUserForcePasswordChange = value.MustChangePassword;
        IsAdminFormEditing = true;
        IsCreatingUser = false;
        ApplyRoleSelections(value.Roles ?? []);
        if (OrganizationOptions.FirstOrDefault(item => item.Id == value.OrganizationId) is { } organizationOption)
        {
            SelectedOrganizationOption = organizationOption;
        }
    }

    public ObservableCollection<string> SupportedFormats { get; } = new() { "VVDI MB Tool", "CGDI MB" };
    public ObservableCollection<string> ResearchSourceFiles { get; } = new() { "EIS dump", "Key file", "Compare dump" };
    public ObservableCollection<string> ResearchSearchModes { get; } = new() { "Exact", "Reversed", "BytePairSwapped", "FourByteWordReversed", "Xor" };
    public ObservableCollection<string> ResearchConfidenceValues { get; } = new() { "Unknown", "Suspected", "Probable", "Verified", "Low" };

    [RelayCommand]
    private async Task LoadCurrentUser()
    {
        try
        {
            var currentUser = await _apiClient.GetCurrentUserAsync();
            CurrentUserDisplay = string.IsNullOrWhiteSpace(currentUser.DisplayName) ? currentUser.Email : currentUser.DisplayName;
            IsAdministrator = currentUser.IsAdministrator;
            if (IsAdministrator)
            {
                await LoadAdminUsersAsync();
                await LoadOrganizationsAsync();
                await LoadRoleOptionsAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Load current user failed: {ex}");
            CurrentUserDisplay = "Not signed in";
            IsAdministrator = false;
            AdminStatus = $"Unable to load current user. {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshAdminUsers()
    {
        if (!IsAdministrator)
        {
            return;
        }

        try
        {
            await LoadAdminUsersAsync();
            await LoadOrganizationsAsync();
            await LoadRoleOptionsAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Administration refresh failed: {ex}");
            AdminStatus = $"Unable to refresh administrator users. {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CreateOrUpdateOrganization()
    {
        if (!IsAdministrator || IsSubmittingAdminAction)
        {
            return;
        }

        try
        {
            IsSubmittingAdminAction = true;
            if (string.IsNullOrWhiteSpace(OrganizationName))
            {
                AdminStatus = "Organization name is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(OrganizationContactEmail))
            {
                AdminStatus = "Contact email is required.";
                return;
            }

            if (IsCreatingOrganization || SelectedOrganization is null)
            {
                var request = new CreateOrganizationRequestDto
                {
                    Name = OrganizationName,
                    ContactEmail = OrganizationContactEmail,
                    Country = OrganizationCountry,
                    IsActive = OrganizationIsActive,
                    LicenseType = OrganizationLicenseType,
                    MaxUsers = int.TryParse(OrganizationMaxUsers, out var maxUsers) ? maxUsers : 4,
                    LicenseExpirationUtc = string.IsNullOrWhiteSpace(OrganizationLicenseExpiration) ? null : DateTimeOffset.Parse(OrganizationLicenseExpiration)
                };

                await _apiClient.CreateOrganizationAsync(request);
                AdminStatus = "Organization created.";
            }
            else
            {
                var request = new UpdateOrganizationRequestDto
                {
                    Name = OrganizationName,
                    ContactEmail = OrganizationContactEmail,
                    Country = OrganizationCountry,
                    IsActive = OrganizationIsActive,
                    LicenseType = OrganizationLicenseType,
                    MaxUsers = int.TryParse(OrganizationMaxUsers, out var maxUsers) ? maxUsers : 4,
                    LicenseExpirationUtc = string.IsNullOrWhiteSpace(OrganizationLicenseExpiration) ? null : DateTimeOffset.Parse(OrganizationLicenseExpiration)
                };

                await _apiClient.UpdateOrganizationAsync(SelectedOrganization.Id, request);
                AdminStatus = "Organization updated.";
            }

            ResetOrganizationForm();
            await LoadOrganizationsAsync();
        }
        catch (Exception ex)
        {
            AdminStatus = $"Unable to save organization. {ex.Message}";
        }
        finally
        {
            IsSubmittingAdminAction = false;
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedOrganization()
    {
        if (!IsAdministrator || SelectedOrganization is null || IsSubmittingAdminAction)
        {
            return;
        }

        try
        {
            IsSubmittingAdminAction = true;
            await _apiClient.DeleteOrganizationAsync(SelectedOrganization.Id);
            AdminStatus = "Organization deleted.";
            ResetOrganizationForm();
            await LoadOrganizationsAsync();
        }
        catch (Exception ex)
        {
            AdminStatus = $"Unable to delete organization. {ex.Message}";
        }
        finally
        {
            IsSubmittingAdminAction = false;
        }
    }

    [RelayCommand]
    private async Task CreateOrUpdateUser()
    {
        if (!IsAdministrator || IsSubmittingAdminAction)
        {
            return;
        }

        try
        {
            IsSubmittingAdminAction = true;
            if (string.IsNullOrWhiteSpace(AdminUserEmail))
            {
                AdminStatus = "Email is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(AdminUserDisplayName))
            {
                AdminStatus = "Display name is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(AdminUserOrganizationId))
            {
                AdminStatus = "Select an organization.";
                return;
            }

            var roles = RoleOptions.Where(option => option.CanAssign).Select(option => option.Name).ToList();
            if (roles.Count == 0)
            {
                AdminStatus = "Select at least one role.";
                return;
            }

            SelectedAdminUserRoles = new ObservableCollection<string>(roles);
            if (IsCreatingUser && string.IsNullOrWhiteSpace(AdminUserPassword))
            {
                AdminStatus = "Password is required when creating a user.";
                return;
            }

            var request = new CreateOrUpdateUserRequestDto
            {
                Email = AdminUserEmail,
                DisplayName = AdminUserDisplayName,
                Password = AdminUserPassword,
                OrganizationId = AdminUserOrganizationId,
                Roles = roles,
                IsEnabled = AdminUserIsEnabled,
                MustChangePassword = AdminUserForcePasswordChange
            };

            if (IsCreatingUser)
            {
                await _apiClient.CreateUserAsync(request);
                AdminStatus = "User created.";
            }
            else
            {
                await _apiClient.UpdateUserAsync(SelectedAdminUser?.Id ?? string.Empty, request);
                AdminStatus = "User updated.";
            }

            ResetUserForm();
            await LoadAdminUsersAsync();
        }
        catch (Exception ex)
        {
            AdminStatus = $"Unable to save user. {ex.Message}";
        }
        finally
        {
            IsSubmittingAdminAction = false;
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedUser()
    {
        if (!IsAdministrator || SelectedAdminUser is null || IsSubmittingAdminAction)
        {
            return;
        }

        try
        {
            IsSubmittingAdminAction = true;
            await _apiClient.DeleteUserAsync(SelectedAdminUser.Id);
            AdminStatus = "User deleted.";
            ResetUserForm();
            await LoadAdminUsersAsync();
        }
        catch (Exception ex)
        {
            AdminStatus = $"Unable to delete user. {ex.Message}";
        }
        finally
        {
            IsSubmittingAdminAction = false;
        }
    }

    [RelayCommand]
    private async Task ResetSelectedUserPassword()
    {
        if (!IsAdministrator || SelectedAdminUser is null || IsSubmittingAdminAction)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(AdminUserPassword))
        {
            AdminStatus = "Enter a new password before resetting.";
            return;
        }

        try
        {
            IsSubmittingAdminAction = true;
            var response = await _apiClient.ResetUserPasswordAsync(SelectedAdminUser.Id, new ResetPasswordRequestDto
            {
                NewPassword = AdminUserPassword,
                ForcePasswordChange = AdminUserForcePasswordChange
            });
            AdminStatus = response?.Message ?? "Password reset.";
            await LoadAdminUsersAsync();
        }
        catch (Exception ex)
        {
            AdminStatus = $"Unable to reset password. {ex.Message}";
        }
        finally
        {
            IsSubmittingAdminAction = false;
        }
    }

    [RelayCommand]
    private async Task ToggleSelectedUserPasswordRequirement()
    {
        if (!IsAdministrator || SelectedAdminUser is null)
        {
            return;
        }

        try
        {
            var response = await _apiClient.SetUserPasswordChangeRequirementAsync(SelectedAdminUser.Id, new ForcePasswordChangeRequestDto
            {
                RequirePasswordChange = !AdminUserForcePasswordChange
            });
            AdminStatus = response?.Message ?? "Password requirement updated.";
            await LoadAdminUsersAsync();
        }
        catch (Exception ex)
        {
            AdminStatus = $"Unable to update password requirement. {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DisableSelectedAdminUser()
    {
        if (!IsAdministrator || SelectedAdminUser is null)
        {
            return;
        }

        try
        {
            var response = await _apiClient.DisableUserAsync(SelectedAdminUser.Id);
            AdminStatus = response?.Message ?? "User status updated.";
            await LoadAdminUsersAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Administration disable failed: {ex}");
            AdminStatus = $"Unable to disable user. {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task EnableSelectedAdminUser()
    {
        if (!IsAdministrator || SelectedAdminUser is null)
        {
            return;
        }

        try
        {
            var response = await _apiClient.EnableUserAsync(SelectedAdminUser.Id);
            AdminStatus = response?.Message ?? "User status updated.";
            await LoadAdminUsersAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Administration enable failed: {ex}");
            AdminStatus = $"Unable to enable user. {ex.Message}";
        }
    }

    private async Task LoadAdminUsersAsync()
    {
        try
        {
            var response = await _apiClient.GetAdminUsersAsync();
            var items = response?.Items?
                .Where(item => item is not null)
                .OrderBy(item => item.Email, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<AdminUserListItemDto>();

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                AdminUsers = new ObservableCollection<AdminUserListItemDto>(items);
                if (SelectedAdminUser is not null)
                {
                    var currentSelection = items.FirstOrDefault(item => item.Id == SelectedAdminUser.Id);
                    SelectedAdminUser = currentSelection;
                }
                else if (items.Count > 0)
                {
                    SelectedAdminUser = items.First();
                }
                else
                {
                    SelectedAdminUser = null;
                }
                AdminStatus = string.Empty;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Administration load failed: {ex}");
            AdminStatus = $"Unable to load administrator users. {ex.Message}";
        }
    }

    private async Task LoadOrganizationsAsync()
    {
        try
        {
            var response = await _apiClient.GetOrganizationsAsync();
            var items = response?.Items?
                .Where(item => item is not null)
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<OrganizationSummaryDto>();

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Organizations = new ObservableCollection<OrganizationSummaryDto>(items);
                OrganizationOptions = new ObservableCollection<OrganizationOptionDto>(items.Select(item => new OrganizationOptionDto(item.Id, item.Name)));
                if (SelectedOrganization is not null)
                {
                    var currentSelection = items.FirstOrDefault(item => item.Id == SelectedOrganization.Id);
                    SelectedOrganization = currentSelection;
                }
                else if (items.Count > 0)
                {
                    SelectedOrganization = items.First();
                }
                else
                {
                    SelectedOrganization = null;
                }

                if (SelectedOrganizationOption is null && OrganizationOptions.Any())
                {
                    SelectedOrganizationOption = OrganizationOptions.FirstOrDefault(option => option.Id == (SelectedOrganization?.Id ?? string.Empty)) ?? OrganizationOptions.First();
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Organizations load failed: {ex}");
            AdminStatus = $"Unable to load organizations. {ex.Message}";
        }
    }

    private async Task LoadRoleOptionsAsync()
    {
        try
        {
            var options = await _apiClient.GetRoleOptionsAsync();
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                RoleOptions = new ObservableCollection<RoleOptionDto>(options);
                ApplyRoleSelections(SelectedAdminUserRoles);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Role options load failed: {ex}");
            AdminStatus = $"Unable to load role options. {ex.Message}";
        }
    }

    [RelayCommand]
    private void StartCreatingOrganization()
    {
        IsCreatingOrganization = true;
        SelectedOrganization = null;
        OrganizationName = string.Empty;
        OrganizationContactEmail = string.Empty;
        OrganizationCountry = string.Empty;
        OrganizationIsActive = true;
        OrganizationLicenseType = "Standard";
        OrganizationMaxUsers = "4";
        OrganizationLicenseExpiration = string.Empty;
    }

    [RelayCommand]
    private void StartCreatingUser()
    {
        IsCreatingUser = true;
        IsAdminFormEditing = false;
        AdminUserEmail = string.Empty;
        AdminUserDisplayName = string.Empty;
        AdminUserPassword = string.Empty;
        AdminUserIsEnabled = true;
        AdminUserForcePasswordChange = false;
        SelectedAdminUserRoles = new ObservableCollection<string>();
        ApplyRoleSelections(Array.Empty<string>());
        if (SelectedOrganizationOption is null && OrganizationOptions.Any())
        {
            SelectedOrganizationOption = OrganizationOptions.FirstOrDefault(option => option.Id == (SelectedOrganization?.Id ?? string.Empty)) ?? OrganizationOptions.First();
        }
        AdminUserOrganizationId = SelectedOrganizationOption?.Id ?? string.Empty;
        if (SelectedOrganizationOption is not null)
        {
            SelectedOrganizationOption = OrganizationOptions.FirstOrDefault(option => option.Id == SelectedOrganizationOption.Id) ?? SelectedOrganizationOption;
        }
    }

    private void ResetOrganizationForm()
    {
        IsCreatingOrganization = false;
        SelectedOrganization = null;
        OrganizationName = string.Empty;
        OrganizationContactEmail = string.Empty;
        OrganizationCountry = string.Empty;
        OrganizationIsActive = true;
        OrganizationLicenseType = "Standard";
        OrganizationMaxUsers = "4";
        OrganizationLicenseExpiration = string.Empty;
    }

    private void ResetUserForm()
    {
        IsCreatingUser = false;
        IsAdminFormEditing = false;
        AdminUserEmail = string.Empty;
        AdminUserDisplayName = string.Empty;
        AdminUserPassword = string.Empty;
        AdminUserOrganizationId = string.Empty;
        SelectedAdminUserRoles = new ObservableCollection<string>();
        ApplyRoleSelections(Array.Empty<string>());
        AdminUserIsEnabled = true;
        AdminUserForcePasswordChange = false;
    }

    private void ApplyRoleSelections(IEnumerable<string> selectedRoles)
    {
        var selected = new HashSet<string>(selectedRoles.Where(role => !string.IsNullOrWhiteSpace(role)).Select(role => role.Trim()), StringComparer.OrdinalIgnoreCase);
        foreach (var roleOption in RoleOptions)
        {
            roleOption.CanAssign = selected.Contains(roleOption.Name);
        }

        SelectedAdminUserRoles = new ObservableCollection<string>(RoleOptions.Where(option => option.CanAssign).Select(option => option.Name));
    }

    [RelayCommand]
    private async Task OpenDump()
    {
        var result = await PickFileAsync("Open Mercedes EIS dump");
        if (result is null)
        {
            return;
        }

        try
        {
            var bytes = LoadLocalFile(result.Value.Path);
            if (bytes.Length != 256)
            {
                Status = "Expected a 256-byte dump.";
                return;
            }

            SelectedFileName = Path.GetFileName(result.Value.Path);
            SelectedFilePath = result.Value.Path;
            CustomerName = string.Empty;
            RawHexText = BuildRawHexText(bytes);
            AnalysisSummary = "File loaded locally. Use Analyze to send it to the server.";
            UploadSummary = string.Empty;
            Status = $"Loaded {SelectedFileName}";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AnalyzeDump()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            Status = "Open a dump file before analyzing it.";
            return;
        }

        try
        {
            IsBusy = true;
            var bytes = LoadLocalFile(SelectedFilePath);
            var response = await _apiClient.AnalyzeDumpAsync(bytes, SelectedFileName);
            var details = response.AnalysisDetails;
            Vin = DisplayValue(response.DetectedVin);
            DetectedFormat = DisplayValue(response.DetectedFormat);
            EisType = details?.EisType ?? "Not mapped";
            Mcu = details?.McuType ?? "Not mapped";
            KeyCount = details?.KeyCount?.ToString() ?? "Not mapped";
            EisPassword = string.IsNullOrWhiteSpace(details?.EisPassword?.Value) ? "Not mapped" : details.EisPassword.Value;
            Ssid = string.IsNullOrWhiteSpace(details?.Ssid?.Value) ? "Not mapped" : details.Ssid.Value;
            ResetEisStateDisplay();
            RawHexText = BuildRawHexText(bytes);
            AnalysisSummary = response.Message;
            UploadSummary = string.Empty;
            ValidationMessage = string.Empty;
            if (!string.IsNullOrWhiteSpace(response.DetectedVin))
            {
                VehicleIdentifier = response.DetectedVin;
                ValidationMessage = "Detected from dump";
                VinConfirmedByUser = false;
            }
            else
            {
                VehicleIdentifier = string.Empty;
                ValidationMessage = "No VIN detected from the dump.";
                VinConfirmedByUser = false;
            }
            Status = response.Status;
            CanConvert = false;
            CanSave = false;
        }
        catch (Exception ex)
        {
            AnalysisSummary = $"Analysis failed: {ex.Message}";
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateUploadAvailability()
    {
        CanUpload = !IsBusy && ServerStatus.Equals("Connected", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(SelectedFileName) && !SelectedFileName.Equals("No file selected", StringComparison.OrdinalIgnoreCase) && VinConfirmedByUser && (!string.IsNullOrWhiteSpace(VehicleIdentifier) || !string.IsNullOrWhiteSpace(RegistrationNumber));
    }

    [RelayCommand]
    private async Task UploadDump()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            Status = "Open a dump file before uploading it.";
            return;
        }

        if (!VinConfirmedByUser)
        {
            Status = "Provide either a VIN or registration number and confirm it before uploading.";
            return;
        }

        if (string.IsNullOrWhiteSpace(VehicleIdentifier) && string.IsNullOrWhiteSpace(RegistrationNumber))
        {
            Status = "Provide either a VIN or registration number and confirm it before uploading.";
            return;
        }

        try
        {
            IsBusy = true;
            var bytes = LoadLocalFile(SelectedFilePath);
            var response = await _apiClient.UploadDumpAsync(bytes, SelectedFileName, VehicleIdentifier, RegistrationNumber, VinConfirmedByUser, CustomerName);
            var details = response.AnalysisDetails;
            UploadSummary = $"Uploaded to server: {response.Status} | {response.Message}";
            if (!string.IsNullOrWhiteSpace(response.CustomerName))
            {
                CustomerName = response.CustomerName;
            }
            if (details is not null)
            {
                UploadSummary += $"{Environment.NewLine}EIS type: {details.EisType ?? "Not mapped"}; Key count: {details.KeyCount?.ToString() ?? "Not mapped"}";
                EisPassword = string.IsNullOrWhiteSpace(details.EisPassword?.Value) ? "Not mapped" : details.EisPassword.Value;
                Ssid = string.IsNullOrWhiteSpace(details.Ssid?.Value) ? "Not mapped" : details.Ssid.Value;
                ResetEisStateDisplay();
            }
            Status = response.Status;
            await RefreshUploadedFilesAsync();
        }
        catch (Exception ex)
        {
            UploadSummary = $"Upload failed: {ex.Message}";
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshUploadedFiles()
    {
        await RefreshUploadedFilesAsync();
    }

    [RelayCommand]
    private async Task SearchMyFiles()
    {
        await RefreshUploadedFilesAsync();
    }

    [RelayCommand(CanExecute = nameof(CanOpenDetails))]
    private async Task OpenDetails()
    {
        if (SelectedStoredFile is null)
        {
            return;
        }

        try
        {
            var details = await _apiClient.GetStoredFileDetailsAsync(SelectedStoredFile.Id);
            MyFilesDetails = $"File: {details.OriginalFileName}{Environment.NewLine}Format: {details.DetectedFormat}{Environment.NewLine}VIN: {details.DetectedVin ?? "Not mapped"}{Environment.NewLine}Customer name: {details.CustomerName ?? "Not provided"}{Environment.NewLine}Registration: {details.RegistrationNumber ?? "Not provided"}{Environment.NewLine}EIS type: {details.EisType ?? "Not mapped"}{Environment.NewLine}MCU: {details.McuType ?? "Not mapped"}{Environment.NewLine}Key count: {details.KeyCount?.ToString() ?? "Not mapped"}{Environment.NewLine}EIS password: {details.EisPassword ?? "Not mapped"}{Environment.NewLine}SSID: {details.Ssid ?? "Not mapped"}";
            PopulateWorkspaceFromDetails(details);
            SelectedMainTabIndex = 0;
            Status = $"Loaded details for {details.OriginalFileName}.";
        }
        catch (Exception ex)
        {
            MyFilesDetails = $"Details failed: {ex.Message}";
            Status = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadIntoWorkspace))]
    private async Task LoadIntoWorkspace()
    {
        if (SelectedStoredFile is null)
        {
            return;
        }

        try
        {
            IsLoadingStoredFile = true;
            var bytes = await _apiClient.DownloadStoredFileAsync(SelectedStoredFile.Id);
            var sha = ComputeSha256(bytes);
            if (!string.Equals(sha, SelectedStoredFile.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Downloaded file SHA-256 did not match the stored metadata.");
            }

            var details = await _apiClient.GetStoredFileDetailsAsync(SelectedStoredFile.Id);
            PopulateWorkspaceFromDetails(details, bytes);
            CurrentWorkspaceStoredFileId = SelectedStoredFile.Id;
            SelectedMainTabIndex = 0;
            Status = $"Loaded {SelectedStoredFile.OriginalFileName} from server.";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsLoadingStoredFile = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDownloadOriginal))]
    private async Task DownloadOriginal()
    {
        if (SelectedStoredFile is null)
        {
            return;
        }

        try
        {
            var bytes = await _apiClient.DownloadStoredFileAsync(SelectedStoredFile.Id);
            var sha = ComputeSha256(bytes);
            if (!string.Equals(sha, SelectedStoredFile.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Downloaded file SHA-256 did not match the stored metadata.");
            }

            var window = (App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as Window;
            var file = await window!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save downloaded dump",
                SuggestedFileName = SelectedStoredFile.OriginalFileName,
                DefaultExtension = ".bin"
            });

            if (file is null)
            {
                Status = "Download cancelled.";
                return;
            }

            await File.WriteAllBytesAsync(file.Path.LocalPath, bytes);
            MyFilesDetails = $"Downloaded {SelectedStoredFile.OriginalFileName} to {file.Path.LocalPath}";
            Status = $"Downloaded {SelectedStoredFile.OriginalFileName}.";
        }
        catch (Exception ex)
        {
            MyFilesDetails = $"Download failed: {ex.Message}";
            Status = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanReanalyzeStoredFile))]
    private async Task ReanalyzeStoredFile()
    {
        if (SelectedStoredFile is null)
        {
            return;
        }

        try
        {
            var details = await _apiClient.ReanalyzeStoredFileAsync(SelectedStoredFile.Id);
            MyFilesDetails = $"Reanalyzed: {details.DetectedFormat} | VIN: {details.DetectedVin ?? "Not mapped"}";
            Status = "Stored file reanalyzed.";
            await RefreshUploadedFilesAsync();
        }
        catch (Exception ex)
        {
            MyFilesDetails = $"Reanalysis failed: {ex.Message}";
            Status = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCompareStoredFile))]
    private void SetCompareA()
    {
        if (SelectedStoredFile is null)
        {
            return;
        }

        Status = $"Compare A set to {SelectedStoredFile.OriginalFileName}";
    }

    [RelayCommand(CanExecute = nameof(CanCompareStoredFile))]
    private void SetCompareB()
    {
        if (SelectedStoredFile is null)
        {
            return;
        }

        Status = $"Compare B set to {SelectedStoredFile.OriginalFileName}";
    }

    [RelayCommand(CanExecute = nameof(CanCopyVinValue))]
    private async Task CopyVin()
    {
        if (SelectedStoredFile is null)
        {
            return;
        }

        await CopyValueAsync(SelectedStoredFile.UserProvidedVin ?? SelectedStoredFile.DetectedVin, $"VIN copied for {SelectedStoredFile.OriginalFileName}");
    }

    [RelayCommand(CanExecute = nameof(CanCopyRegistrationValue))]
    private async Task CopyRegistration()
    {
        if (SelectedStoredFile is null)
        {
            return;
        }

        await CopyValueAsync(SelectedStoredFile.RegistrationNumber, $"Registration copied for {SelectedStoredFile.OriginalFileName}");
    }

    [RelayCommand(CanExecute = nameof(CanCopyEisPasswordValue))]
    private async Task CopyEisPassword()
    {
        if (SelectedStoredFile is null)
        {
            return;
        }

        await CopyValueAsync(SelectedStoredFile.EisPassword, $"EIS password copied for {SelectedStoredFile.OriginalFileName}");
    }

    [RelayCommand(CanExecute = nameof(CanCopySsidValue))]
    private async Task CopySsid()
    {
        if (SelectedStoredFile is null)
        {
            return;
        }

        await CopyValueAsync(SelectedStoredFile.Ssid, $"SSID copied for {SelectedStoredFile.OriginalFileName}");
    }

    [RelayCommand(CanExecute = nameof(CanDeleteStoredFile))]
    private void DeleteStoredFile()
    {
        if (SelectedStoredFile is null)
        {
            return;
        }

        Status = $"Delete requested for {SelectedStoredFile.OriginalFileName}";
    }

    [RelayCommand(CanExecute = nameof(CanRestoreStoredFile))]
    private void RestoreStoredFile()
    {
        if (SelectedStoredFile is null)
        {
            return;
        }

        Status = $"Restore requested for {SelectedStoredFile.OriginalFileName}";
    }

    [RelayCommand]
    private void ConvertDump()
    {
        Status = "Conversion is not implemented yet.";
    }

    [RelayCommand]
    private Task SaveDump()
    {
        Status = "Saving is disabled until conversion is implemented.";
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task LoadResearchDump()
    {
        var result = await PickFileAsync("Open research EIS dump");
        if (result is null)
        {
            return;
        }

        try
        {
            var bytes = LoadLocalFile(result.Value.Path);
            if (bytes.Length != 256)
            {
                Status = "Expected a 256-byte dump.";
                return;
            }

            ResearchEisPath = result.Value.Path;
            Status = $"Loaded EIS dump {Path.GetFileName(result.Value.Path)}";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private async Task LoadResearchKeyFile()
    {
        var result = await PickFileAsync("Open research key file");
        if (result is null)
        {
            return;
        }

        try
        {
            _ = LoadLocalFile(result.Value.Path);
            ResearchKeyPath = result.Value.Path;
            Status = $"Loaded key file {Path.GetFileName(result.Value.Path)}";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private async Task LoadResearchCompareDump()
    {
        var result = await PickFileAsync("Open comparison EIS dump");
        if (result is null)
        {
            return;
        }

        try
        {
            var bytes = LoadLocalFile(result.Value.Path);
            if (bytes.Length != 256)
            {
                Status = "Expected a 256-byte dump.";
                return;
            }

            ResearchComparePath = result.Value.Path;
            Status = $"Loaded comparison dump {Path.GetFileName(result.Value.Path)}";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AnalyzeResearchFolder()
    {
        var window = (App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as Window;
        if (window is null)
        {
            return;
        }

        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select folder for research analysis"
        });

        var folder = folders.FirstOrDefault();
        if (folder is null)
        {
            return;
        }

        try
        {
            ResearchFolderPath = folder.Path.LocalPath;
            var analysis = AnalyzeResearchFolderLocally(folder.Path.LocalPath);
            _lastResearchAnalysis = analysis;

            var lines = new List<string>
            {
                "Folder analysis",
                "---------------"
            };
            foreach (var file in analysis.Files)
            {
                lines.Add($"{file.RelativePath} | size={file.Size} | sha256={file.Sha256} | type={file.DetectedType} | format={file.SourceFormat} | vin={file.VIN} | group={file.DuplicateGroup}");
            }

            if (analysis.DuplicateGroups.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add($"Duplicate groups: {string.Join(", ", analysis.DuplicateGroups)}");
            }

            ResearchFolderAnalysisText = string.Join(Environment.NewLine, lines);
            Status = $"Analyzed {analysis.Files.Count} files";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private void ResearchSearch()
    {
        if (string.IsNullOrWhiteSpace(ResearchEisPath) && string.IsNullOrWhiteSpace(ResearchKeyPath) && string.IsNullOrWhiteSpace(ResearchComparePath))
        {
            ResearchSearchResults = "Load at least one research file before searching.";
            return;
        }

        var sourceBytes = BuildResearchSourceBytes();
        if (sourceBytes is null)
        {
            ResearchSearchResults = "Select a loaded source file first.";
            return;
        }

        if (!TryParseOffset(ResearchStartOffset, out var startOffset))
        {
            ResearchSearchResults = "Enter a valid start offset in decimal or 0x-prefixed hex.";
            return;
        }

        if (!int.TryParse(ResearchLength, out var length) || length < 1 || length > 64)
        {
            ResearchSearchResults = "Sequence length must be between 1 and 64.";
            return;
        }

        if (!TryParseMode(ResearchSelectedMode, ResearchXorValue, out var mode, out var xorValue))
        {
            ResearchSearchResults = "Select a valid search mode and XOR value.";
            return;
        }

        var targets = GetLoadedResearchTargets();
        if (targets.Count == 0)
        {
            ResearchSearchResults = "Load at least one target file before searching.";
            return;
        }

        var formatted = new List<string>();
        var allMatches = new List<ResearchMatchRecord>();
        foreach (var target in targets)
        {
            var matches = SearchResearchSequenceLocally(sourceBytes, target.Bytes, startOffset, length, mode, xorValue);
            allMatches.AddRange(matches);
            foreach (var match in matches)
            {
                formatted.Add($"{target.Name}: offset 0x{match.Offset:X} ({match.Mode}) {Convert.ToHexString(match.MatchedBytes)}");
            }
        }

        _lastResearchMatches = allMatches;
        ResearchSearchResults = string.Join(Environment.NewLine, formatted.Any() ? formatted : new[] { "No matches found." });
    }

    [RelayCommand]
    private void SaveResearchAnnotation()
    {
        if (string.IsNullOrWhiteSpace(ResearchAnnotationName))
        {
            Status = "Provide an annotation name before saving.";
            return;
        }

        if (!int.TryParse(ResearchAnnotationOffset, out var offset))
        {
            Status = "Provide a numeric offset.";
            return;
        }

        if (!int.TryParse(ResearchAnnotationLength, out var length) || length < 1)
        {
            Status = "Provide a positive length.";
            return;
        }

        var annotation = new ResearchAnnotation
        {
            Name = ResearchAnnotationName,
            FileFormat = ResearchAnnotationFileFormat,
            Offset = offset,
            Length = length,
            ByteOrder = ResearchAnnotationByteOrder,
            Notes = ResearchAnnotationNotes,
            Confidence = ParseConfidence(ResearchSelectedConfidence)
        };

        var path = Path.Combine(AppContext.BaseDirectory, "ResearchData", "annotations.json");
        var existing = LoadAnnotations(path);
        existing.Add(annotation);
        SaveAnnotations(path, existing);
        ResearchAnnotationsText = string.Join(Environment.NewLine, existing.Select(item => $"{item.Name} [{item.Confidence}]"));
        Status = "Annotation saved.";
    }

    [RelayCommand]
    private void ExportResearchReport()
    {
        var analysis = _lastResearchAnalysis ?? new ResearchFolderAnalysis();
        var annotationsPath = Path.Combine(AppContext.BaseDirectory, "ResearchData", "annotations.json");
        var annotations = LoadAnnotations(annotationsPath);
        var reportPath = Path.Combine(AppContext.BaseDirectory, "ResearchData", "research-report.txt");
        var report = BuildResearchReport(analysis, annotations, _lastResearchMatches);

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, report, System.Text.Encoding.UTF8);
        ResearchReportPath = reportPath;
        Status = $"Exported research report to {Path.GetFileName(reportPath)}";
    }

    [RelayCommand]
    private async Task OpenFileA()
    {
        var result = await OpenCompareFileAsync();
        if (result is null)
        {
            return;
        }

        _compareABytes = result.Value.Data;
        CompareAPath = result.Value.Path;
        UpdateComparisonUi();
    }

    [RelayCommand]
    private async Task OpenFileB()
    {
        var result = await OpenCompareFileAsync();
        if (result is null)
        {
            return;
        }

        _compareBBytes = result.Value.Data;
        CompareBPath = result.Value.Path;
        UpdateComparisonUi();
    }

    [RelayCommand]
    private void SearchSequence()
    {
        if (_compareABytes is null || _compareBBytes is null)
        {
            SequenceResults = "Load file A and file B before searching.";
            return;
        }

        if (!TryParseOffset(SequenceStartOffset, out var startOffset))
        {
            SequenceResults = "Enter a valid start offset in decimal or 0x-prefixed hex.";
            return;
        }

        if (!int.TryParse(SequenceLength, out var length) || length < 1 || length > 32)
        {
            SequenceResults = "Sequence length must be between 1 and 32.";
            return;
        }

        if (startOffset < 0 || startOffset + length > _compareABytes.Length)
        {
            SequenceResults = "The requested range is outside file A.";
            return;
        }

        SequenceResults = "Server unavailable. Analysis requires an active server connection.";
    }

    private async Task<(string Path, byte[]? Data)?> OpenCompareFileAsync()
    {
        var window = (App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as Window;
        if (window is null)
        {
            return null;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Mercedes EIS dump",
            AllowMultiple = false
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return null;
        }

        try
        {
            var data = File.ReadAllBytes(file.Path.LocalPath);
            if (data.Length != 256)
            {
                Status = "Expected a 256-byte dump.";
                return null;
            }

            return (file.Path.LocalPath, data);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            return null;
        }
    }

    private void UpdateComparisonUi()
    {
        if (_compareABytes is null || _compareBBytes is null)
        {
            CompareText = "Load file A and file B to compare them.";
            CompareSummary = string.Empty;
            CompareOffsets = string.Empty;
            return;
        }

        if (string.IsNullOrWhiteSpace(VehicleIdentifier) || string.IsNullOrWhiteSpace(RegistrationNumber))
        {
            CompareText = "Provide a vehicle identifier and registration number before comparing.";
            CompareSummary = "Provide a vehicle identifier and registration number before comparing.";
            CompareOffsets = string.Empty;
            return;
        }

        CompareText = "Server unavailable. Analysis requires an active server connection.";
        CompareSummary = "Server unavailable. Analysis requires an active server connection.";
        CompareOffsets = string.Empty;
        _ = CompareDumpsAsync(_compareABytes, _compareBBytes);
    }

    private async Task<(string Path, byte[]? Data)?> PickFileAsync(string title)
    {
        var window = (App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as Window;
        if (window is null)
        {
            return null;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return null;
        }

        return (file.Path.LocalPath, File.ReadAllBytes(file.Path.LocalPath));
    }

    private byte[]? BuildResearchSourceBytes()
    {
        if (ResearchSelectedSourceFile == "EIS dump" && File.Exists(ResearchEisPath))
        {
            return File.ReadAllBytes(ResearchEisPath);
        }

        if (ResearchSelectedSourceFile == "Key file" && File.Exists(ResearchKeyPath))
        {
            return File.ReadAllBytes(ResearchKeyPath);
        }

        if (ResearchSelectedSourceFile == "Compare dump" && File.Exists(ResearchComparePath))
        {
            return File.ReadAllBytes(ResearchComparePath);
        }

        return null;
    }

    private List<(string Name, byte[] Bytes)> GetLoadedResearchTargets()
    {
        var targets = new List<(string Name, byte[] Bytes)>();
        if (File.Exists(ResearchEisPath))
        {
            targets.Add(("EIS dump", File.ReadAllBytes(ResearchEisPath)));
        }

        if (File.Exists(ResearchKeyPath))
        {
            targets.Add(("Key file", File.ReadAllBytes(ResearchKeyPath)));
        }

        if (File.Exists(ResearchComparePath))
        {
            targets.Add(("Compare dump", File.ReadAllBytes(ResearchComparePath)));
        }

        return targets;
    }

    private static bool TryParseMode(string? value, string? xorValueText, out ResearchSearchMode mode, out byte xorValue)
    {
        mode = ResearchSearchMode.Exact;
        xorValue = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Equals("Xor", StringComparison.OrdinalIgnoreCase))
        {
            mode = ResearchSearchMode.Xor;
            if (byte.TryParse(xorValueText, NumberStyles.HexNumber, null, out var parsedHex))
            {
                xorValue = parsedHex;
                return true;
            }

            return byte.TryParse(xorValueText, out xorValue);
        }

        return Enum.TryParse(value, true, out mode);
    }

    private static ResearchConfidence ParseConfidence(string value)
    {
        return Enum.TryParse<ResearchConfidence>(value, true, out var confidence) ? confidence : ResearchConfidence.Unknown;
    }

    private static byte[] LoadLocalFile(string path)
    {
        return File.ReadAllBytes(path);
    }

    private static void SaveAnnotations(string path, IEnumerable<ResearchAnnotation> annotations)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = System.Text.Json.JsonSerializer.Serialize(annotations.ToList(), new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    private static List<ResearchAnnotation> LoadAnnotations(string path)
    {
        if (!File.Exists(path))
        {
            return new List<ResearchAnnotation>();
        }

        var json = File.ReadAllText(path, Encoding.UTF8);
        return string.IsNullOrWhiteSpace(json) ? new List<ResearchAnnotation>() : System.Text.Json.JsonSerializer.Deserialize<List<ResearchAnnotation>>(json) ?? new List<ResearchAnnotation>();
    }

    private static string BuildResearchReport(ResearchFolderAnalysis analysis, IEnumerable<ResearchAnnotation> annotations, IEnumerable<ResearchMatchRecord> matches)
    {
        var lines = new List<string> { "Research report", "===============" };
        lines.Add($"Files analyzed: {analysis.Files.Count}");
        if (analysis.DuplicateGroups.Count > 0)
        {
            lines.Add($"Duplicate groups: {string.Join(", ", analysis.DuplicateGroups)}");
        }

        lines.Add(string.Empty);
        lines.Add("Files:");
        foreach (var file in analysis.Files)
        {
            lines.Add($"- {file.RelativePath} | size={file.Size} | sha256={file.Sha256} | type={file.DetectedType} | format={file.SourceFormat} | vin={file.VIN} | group={file.DuplicateGroup}");
        }

        lines.Add(string.Empty);
        lines.Add("Annotations:");
        foreach (var annotation in annotations)
        {
            lines.Add($"- {annotation.Name} | confidence={annotation.Confidence} | offset=0x{annotation.Offset:X} | length={annotation.Length}");
        }

        lines.Add(string.Empty);
        lines.Add("Matches:");
        foreach (var match in matches)
        {
            lines.Add($"- offset=0x{match.Offset:X} | mode={match.Mode} | bytes={Convert.ToHexString(match.MatchedBytes)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static ResearchFolderAnalysis AnalyzeResearchFolderLocally(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException("The selected folder does not exist.");
        }

        var files = Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
            .Where(path => File.Exists(path))
            .Select(path => new FileInfo(path))
            .Where(info => info.Length > 0)
            .OrderBy(info => info.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var analysis = new ResearchFolderAnalysis();
        var shaGroups = new Dictionary<string, List<ResearchFolderFile>>(StringComparer.OrdinalIgnoreCase);
        foreach (var fileInfo in files)
        {
            var bytes = File.ReadAllBytes(fileInfo.FullName);
            var sha = ComputeSha256(bytes);
            var file = new ResearchFolderFile
            {
                FileName = fileInfo.Name,
                RelativePath = fileInfo.FullName.Replace(folderPath, string.Empty).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Size = fileInfo.Length,
                Sha256 = sha,
                DetectedType = bytes.Length == 256 ? "EIS dump" : "Possible key file",
                SourceFormat = bytes.Length == 256 ? "Unknown" : string.Empty,
                VIN = string.Empty,
                Bytes = bytes
            };

            if (shaGroups.TryGetValue(sha, out var group))
            {
                group.Add(file);
            }
            else
            {
                shaGroups[sha] = new List<ResearchFolderFile> { file };
            }

            analysis.Files.Add(file);
        }

        foreach (var group in shaGroups.Where(group => group.Value.Count > 1))
        {
            for (var index = 0; index < group.Value.Count; index++)
            {
                group.Value[index].DuplicateGroup = index + 1;
            }
        }

        analysis.DuplicateGroups = analysis.Files.Where(file => file.DuplicateGroup > 0).Select(file => file.DuplicateGroup).Distinct().OrderBy(value => value).ToList();
        return analysis;
    }

    private static List<ResearchMatchRecord> SearchResearchSequenceLocally(byte[] source, byte[] target, int startOffset, int length, ResearchSearchMode mode, byte xorValue)
    {
        var sequence = source.Skip(startOffset).Take(length).ToArray();
        var results = new List<ResearchMatchRecord>();
        for (var index = 0; index <= target.Length - length; index++)
        {
            var candidate = target.Skip(index).Take(length).ToArray();
            if (MatchesResearchSequence(sequence, candidate, mode, xorValue))
            {
                results.Add(new ResearchMatchRecord { Offset = index, Mode = mode, MatchedBytes = candidate.ToArray() });
            }
        }

        return results;
    }

    private static bool MatchesResearchSequence(byte[] sequence, byte[] candidate, ResearchSearchMode mode, byte xorValue)
    {
        return mode switch
        {
            ResearchSearchMode.Reversed => candidate.SequenceEqual(sequence.Reverse().ToArray()),
            ResearchSearchMode.BytePairSwapped => BytePairSwappedMatches(sequence, candidate),
            ResearchSearchMode.FourByteWordReversed => FourByteWordReversedMatches(sequence, candidate),
            ResearchSearchMode.Xor => candidate.SequenceEqual(sequence.Select(value => (byte)(value ^ xorValue)).ToArray()),
            _ => candidate.SequenceEqual(sequence)
        };
    }

    private static bool BytePairSwappedMatches(byte[] sequence, byte[] candidate)
    {
        if (sequence.Length % 2 != 0 || candidate.Length % 2 != 0)
        {
            return false;
        }

        var expected = new byte[sequence.Length];
        for (var i = 0; i < sequence.Length; i += 2)
        {
            expected[i] = sequence[i + 1];
            expected[i + 1] = sequence[i];
        }

        return candidate.SequenceEqual(expected);
    }

    private static bool FourByteWordReversedMatches(byte[] sequence, byte[] candidate)
    {
        if (sequence.Length < 4 || candidate.Length < 4)
        {
            return false;
        }

        var expected = new byte[sequence.Length];
        Array.Copy(sequence, expected, sequence.Length);

        for (var i = 0; i + 3 < expected.Length; i += 4)
        {
            Array.Reverse(expected, i, 4);
        }

        return candidate.SequenceEqual(expected);
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    private static IMercedesEisApiClient CreateApiClient(string baseUrl)
    {
        var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(5) };
        return new MercedesEisApiClient(httpClient);
    }

    private static string FormatOffsets(IEnumerable<int> offsets)
    {
        var values = offsets.Select(offset => offset.ToString("X2"));
        return values.Any() ? string.Join(", ", values) : "none";
    }

    private static bool TryParseOffset(string? value, out int offset)
    {
        offset = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(value[2..], System.Globalization.NumberStyles.HexNumber, null, out offset);
        }

        return int.TryParse(value, out offset);
    }

    private async Task AnalyzeDumpAsync(byte[] data, string fileName)
    {
        try
        {
            ServerStatus = "Connecting";
            var response = await _apiClient.AnalyzeDumpAsync(data, fileName);
            var details = response.AnalysisDetails;
            Vin = DisplayValue(response.DetectedVin);
            DetectedFormat = DisplayValue(response.DetectedFormat);
            EisType = details?.EisType ?? "Not mapped";
            Mcu = details?.McuType ?? "Not mapped";
            KeyCount = details?.KeyCount?.ToString() ?? "Not mapped";
            RawHexText = BuildRawHexText(data);
            ServerStatus = "Connected";
            Status = response.Status;
            CanConvert = false;
            CanSave = false;
        }
        catch (Exception)
        {
            ServerStatus = "Offline";
            Vin = "Unknown";
            DetectedFormat = "Unknown";
            EisType = "Unknown";
            Mcu = "Unknown";
            KeyCount = "0";
            RawHexText = BuildRawHexText(data);
            Status = "Server unavailable. Analysis requires an active server connection.";
        }
    }

    private async Task RefreshUploadedFilesAsync()
    {
        try
        {
            var response = await _apiClient.GetStoredFilesAsync(MyFilesSearchText, 1, 50);
            var selectedItemId = SelectedStoredFile?.Id;
            _allStoredFiles = response.Items.Select(item => new StoredFileListItemViewModel(item)).ToList();
            ApplyStoredFilesFilter();
            SelectedStoredFile = StoredFiles.FirstOrDefault(item => item.Id == selectedItemId) ?? StoredFiles.FirstOrDefault();
            var lines = response.Items.Select(item => $"{item.Id:N} | {item.OriginalFileName} | VIN={item.UserProvidedVin ?? item.DetectedVin ?? ""} | REG={item.RegistrationNumber ?? ""} | CUST={item.CustomerName ?? ""} | {item.AnalysisStatus} | {item.FileSizeBytes} bytes").ToList();
            UploadedFilesSummary = lines.Any() ? string.Join(Environment.NewLine, lines) : "No uploaded files yet.";
        }
        catch (Exception ex)
        {
            UploadedFilesSummary = $"Unable to load uploads: {ex.Message}";
        }
    }

    private async Task CompareDumpsAsync(byte[] left, byte[] right)
    {
        try
        {
            ServerStatus = "Connecting";
            var response = await _apiClient.CompareDumpsAsync(left, right, "left.bin", "right.bin", VehicleIdentifier, RegistrationNumber);
            CompareText = $"Total differing bytes: {response.TotalDifferences}{Environment.NewLine}Offsets: {string.Join(", ", response.DifferingOffsets)}";
            CompareSummary = "Compared via server";
            CompareOffsets = $"Differing bytes: {response.TotalDifferences} | Offsets: {FormatOffsets(response.DifferingOffsets)}";
            ServerStatus = "Connected";
        }
        catch (Exception)
        {
            ServerStatus = "Offline";
            CompareText = "Server unavailable. Analysis requires an active server connection.";
            CompareSummary = "Server unavailable. Analysis requires an active server connection.";
            CompareOffsets = string.Empty;
        }
    }

    private async Task RefreshServerStatusAsync()
    {
        try
        {
            ServerStatus = "Connecting";
            var response = await _apiClient.GetHealthAsync();
            ServerStatus = response.IsHealthy ? "Connected" : "Offline";
            ConnectionReason = response.Status;
            LastChecked = DateTime.Now.ToString("HH:mm:ss");
        }
        catch (Exception ex)
        {
            ServerStatus = "Offline";
            ConnectionReason = ex.Message;
            LastChecked = DateTime.Now.ToString("HH:mm:ss");
        }
    }

    private void ApplyStoredFilesFilter()
    {
        var query = MyFilesSearchText?.Trim() ?? string.Empty;
        var items = string.IsNullOrWhiteSpace(query)
            ? _allStoredFiles
            : _allStoredFiles.Where(item =>
                item.OriginalFileName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (item.UserProvidedVin?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.DetectedVin?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.RegistrationNumber?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.CustomerName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.DetectedFormat?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.EisType?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.McuType?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.AnalysisStatus?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

        var selectedId = SelectedStoredFile?.Id;
        StoredFiles = new ObservableCollection<StoredFileListItemViewModel>(items);
        SelectedStoredFile = StoredFiles.FirstOrDefault(item => item.Id == selectedId) ?? StoredFiles.FirstOrDefault();
        UpdateStoredFileCommandStates();
    }

    private bool CanOpenDetails()
    {
        return SelectedStoredFile is not null && !IsBusy && !IsLoadingStoredFile && ServerStatus.Equals("Connected", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanLoadIntoWorkspace()
    {
        return SelectedStoredFile is not null && !IsBusy && !IsLoadingStoredFile && ServerStatus.Equals("Connected", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanDownloadOriginal()
    {
        return SelectedStoredFile is not null && !IsBusy && !IsLoadingStoredFile && ServerStatus.Equals("Connected", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanReanalyzeStoredFile()
    {
        return SelectedStoredFile is not null && !IsBusy && !IsLoadingStoredFile && ServerStatus.Equals("Connected", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanCompareStoredFile()
    {
        return SelectedStoredFile is not null && !IsBusy && !IsLoadingStoredFile && ServerStatus.Equals("Connected", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanCopyStoredFileValue()
    {
        return SelectedStoredFile is not null && !IsBusy && !IsLoadingStoredFile && ServerStatus.Equals("Connected", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanCopyVinValue()
    {
        return CanCopyStoredFileValue() && !string.IsNullOrWhiteSpace(SelectedStoredFile?.UserProvidedVin ?? SelectedStoredFile?.DetectedVin);
    }

    private bool CanCopyRegistrationValue()
    {
        return CanCopyStoredFileValue() && !string.IsNullOrWhiteSpace(SelectedStoredFile?.RegistrationNumber);
    }

    private bool CanCopyEisPasswordValue()
    {
        return CanCopyStoredFileValue() && !string.IsNullOrWhiteSpace(SelectedStoredFile?.EisPassword);
    }

    private bool CanCopySsidValue()
    {
        return CanCopyStoredFileValue() && !string.IsNullOrWhiteSpace(SelectedStoredFile?.Ssid);
    }

    private async Task CopyValueAsync(string? value, string successMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Status = "Nothing to copy.";
            return;
        }

        try
        {
            await _clipboardService.SetTextAsync(value);
            Status = successMessage;
        }
        catch (Exception ex)
        {
            Status = $"Clipboard unavailable: {ex.Message}";
        }
    }

    private bool CanDeleteStoredFile()
    {
        return SelectedStoredFile is not null && !SelectedStoredFile.IsDeleted && !IsBusy && !IsLoadingStoredFile && ServerStatus.Equals("Connected", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanRestoreStoredFile()
    {
        return SelectedStoredFile is not null && SelectedStoredFile.IsDeleted && !IsBusy && !IsLoadingStoredFile && ServerStatus.Equals("Connected", StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateStoredFileCommandStates()
    {
        OpenDetailsCommand.NotifyCanExecuteChanged();
        LoadIntoWorkspaceCommand.NotifyCanExecuteChanged();
        DownloadOriginalCommand.NotifyCanExecuteChanged();
        ReanalyzeStoredFileCommand.NotifyCanExecuteChanged();
        SetCompareACommand.NotifyCanExecuteChanged();
        SetCompareBCommand.NotifyCanExecuteChanged();
        CopyVinCommand.NotifyCanExecuteChanged();
        CopyRegistrationCommand.NotifyCanExecuteChanged();
        CopyEisPasswordCommand.NotifyCanExecuteChanged();
        CopySsidCommand.NotifyCanExecuteChanged();
        DeleteStoredFileCommand.NotifyCanExecuteChanged();
        RestoreStoredFileCommand.NotifyCanExecuteChanged();
    }

    private void PopulateWorkspaceFromDetails(StoredFileDetailsDto details, byte[]? bytes = null)
    {
        SelectedFileName = details.OriginalFileName;
        SelectedFileBytes = bytes ?? SelectedFileBytes;
        CustomerName = details.CustomerName ?? string.Empty;
        SelectedFileSize = bytes?.Length ?? details.FileSizeBytes;
        SelectedFileSha256 = details.Sha256;
        RawHexText = bytes is null ? "No raw dump available." : BuildRawHexText(bytes);
        DetectedFormat = DisplayValue(details.DetectedFormat);
        Vin = DisplayValue(details.DetectedVin);
        VinStatus = details.VinStatus;
        EisType = DisplayValue(details.EisType);
        Mcu = DisplayValue(details.McuType);
        KeyCount = details.KeyCount?.ToString() ?? "Not mapped";
        EisPassword = string.IsNullOrWhiteSpace(details.EisPassword) ? "Not mapped" : details.EisPassword;
        Ssid = string.IsNullOrWhiteSpace(details.Ssid) ? "Not mapped" : details.Ssid;
        KeySlots = new ObservableCollection<KeySlotDto>(details.Keys);
        ResetEisStateDisplay();
        AnalysisSummary = $"Loaded {details.OriginalFileName} from server.";
    }

    public string InitializedDisplay => FormatTriState(Initialized);
    public string PersonalizedDisplay => FormatTriState(Personalized);
    public string TpClearedDisplay => FormatTriState(TpCleared);
    public string ActivatedDisplay => FormatTriState(Activated);
    public string DealerEisDisplay => FormatTriState(DealerEis);
    public string Fbs4Display => FormatTriState(Fbs4);

    private void ResetEisStateDisplay()
    {
        Initialized = null;
        Personalized = null;
        TpCleared = null;
        Activated = null;
        DealerEis = null;
        Fbs4 = null;
    }

    private static string FormatTriState(bool? value)
    {
        return value switch
        {
            true => "✔",
            false => "✖",
            _ => "—"
        };
    }

    private static string DisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
    }

    private static string BuildRawHexText(byte[] data)
    {
        if (data is null || data.Length != 256)
        {
            return "No raw dump available.";
        }

        var lines = new List<string>();
        for (var offset = 0; offset < data.Length; offset += 16)
        {
            var chunk = data.Skip(offset).Take(16).ToArray();
            var hex = string.Join(" ", chunk.Select(b => b.ToString("X2")));
            var ascii = new string(chunk.Select(b => b >= 0x20 && b < 0x7F ? (char)b : '.').ToArray());
            lines.Add($"{offset:D4}  {hex.PadRight(47)} {ascii}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildComparisonText(byte[] left, byte[] right, CompareDumpsResponse comparison)
    {
        if (left is null || right is null || left.Length != 256 || right.Length != 256)
        {
            return "Load two 256-byte dumps to compare them.";
        }

        var lines = new List<string>
        {
            $"Total differing bytes: {comparison.TotalDifferences}",
            $"Offsets: {string.Join(", ", comparison.DifferingOffsets)}"
        };

        return string.Join(Environment.NewLine, lines);
    }

    public sealed class StoredFileListItemViewModel
    {
        public StoredFileListItemViewModel(StoredFileListItemDto item)
        {
            Id = item.Id;
            OriginalFileName = item.OriginalFileName;
            UploadedAtUtc = item.UploadedAtUtc;
            UserProvidedVin = item.UserProvidedVin;
            DetectedVin = item.DetectedVin;
            RegistrationNumber = item.RegistrationNumber;
            CustomerName = item.CustomerName;
            DetectedFormat = item.DetectedFormat;
            EisType = item.EisType;
            McuType = item.McuType;
            KeyCount = item.KeyCount;
            EisPassword = item.EisPassword;
            Ssid = item.Ssid;
            KeyPasswordsFound = item.KeyPasswordsFound;
            AnalysisStatus = item.AnalysisStatus;
            ParserVersion = item.ParserVersion;
            FileSizeBytes = item.FileSizeBytes;
            Sha256 = item.Sha256;
            IsDeleted = item.IsDeleted;
        }

        public Guid Id { get; }
        public string OriginalFileName { get; }
        public DateTimeOffset UploadedAtUtc { get; }
        public string? UserProvidedVin { get; }
        public string? DetectedVin { get; }
        public string? RegistrationNumber { get; }
        public string? CustomerName { get; }
        public string DetectedFormat { get; }
        public string? EisType { get; }
        public string? McuType { get; }
        public int? KeyCount { get; }
        public string? EisPassword { get; }
        public string? Ssid { get; }
        public int KeyPasswordsFound { get; }
        public string AnalysisStatus { get; }
        public string ParserVersion { get; }
        public long FileSizeBytes { get; }
        public string Sha256 { get; }
        public bool IsDeleted { get; }
    }

    private sealed class ResearchFolderAnalysis
    {
        public List<ResearchFolderFile> Files { get; } = new();
        public List<int> DuplicateGroups { get; set; } = new();
    }

    private sealed class ResearchFolderFile
    {
        public string FileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public string DetectedType { get; set; } = string.Empty;
        public string SourceFormat { get; set; } = string.Empty;
        public string VIN { get; set; } = string.Empty;
        public int DuplicateGroup { get; set; }
        public byte[]? Bytes { get; set; }
    }

    private sealed class ResearchMatchRecord
    {
        public int Offset { get; set; }
        public ResearchSearchMode Mode { get; set; }
        public byte[] MatchedBytes { get; set; } = Array.Empty<byte>();
    }

    private sealed class ResearchAnnotation
    {
        public string Name { get; set; } = string.Empty;
        public string FileFormat { get; set; } = string.Empty;
        public int Offset { get; set; }
        public int Length { get; set; }
        public string ByteOrder { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public ResearchConfidence Confidence { get; set; } = ResearchConfidence.Unknown;
    }

    private enum ResearchSearchMode
    {
        Exact,
        Reversed,
        BytePairSwapped,
        FourByteWordReversed,
        Xor
    }

    private enum ResearchConfidence
    {
        Unknown,
        Suspected,
        Probable,
        Verified,
        Low
    }
}

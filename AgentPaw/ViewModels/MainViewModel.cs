using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgentPaw.Services;

namespace AgentPaw.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AuthService _authService;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string? _profileImageUrl;

    [ObservableProperty]
    private object? _currentPage;

    public MainViewModel(AuthService authService)
    {
        _authService = authService;
    }

    public void SetAuthenticated(SessionInfo session)
    {
        IsAuthenticated = true;
        DisplayName = session.DisplayName;
        Email = session.Email;
        ProfileImageUrl = session.ProfileImageUrl;
    }

    public void ClearAuthentication()
    {
        IsAuthenticated = false;
        DisplayName = string.Empty;
        Email = string.Empty;
        ProfileImageUrl = null;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        _authService.ClearPersistedSession();
        ClearAuthentication();
    }
}

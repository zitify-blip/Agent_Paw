using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using AgentPaw.Data;
using AgentPaw.Models;
using AgentPaw.Services;

namespace AgentPaw.ViewModels;

public partial class ApiSettingsViewModel : ObservableObject
{
    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;
    private readonly ConfigLoaderService _configLoader;

    [ObservableProperty] private string _projectId = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _saveMessage;

    // Selected persona for editing
    [ObservableProperty] private Persona? _selectedPersona;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isSaving;

    // Edit fields
    [ObservableProperty] private string _editPrimaryModel = string.Empty;
    [ObservableProperty] private string? _editFallbackModel;
    [ObservableProperty] private float _editTemperature = 0.7f;
    [ObservableProperty] private int _editMaxTokens = 4096;

    public ObservableCollection<Persona> Personas { get; } = [];

    public static readonly string[] KnownModels =
    [
        "claude-opus-4-7",
        "claude-sonnet-4-6",
        "claude-haiku-4-5-20251001",
        "gemini-2.5-pro",
        "gemini-2.5-flash",
    ];

    public ApiSettingsViewModel(IDbContextFactory<AgentPawDbContext> dbFactory, ConfigLoaderService configLoader)
    {
        _dbFactory = dbFactory;
        _configLoader = configLoader;
    }

    public async Task LoadAsync(string projectId)
    {
        ProjectId = projectId;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var personas = await _configLoader.ListPersonasAsync(ProjectId);
            Personas.Clear();
            foreach (var p in personas)
                Personas.Add(p);
            SelectedPersona = null;
            IsEditing = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.InnerException?.Message ?? ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectPersona(Persona persona)
    {
        SelectedPersona = persona;
        EditPrimaryModel = persona.PrimaryModel ?? "claude-sonnet-4-6";
        EditFallbackModel = persona.FallbackModel;
        EditTemperature = persona.Temperature;
        EditMaxTokens = persona.MaxTokens;
        IsEditing = true;
        SaveMessage = null;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        SelectedPersona = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedPersona == null || IsSaving) return;
        IsSaving = true;
        ErrorMessage = null;
        SaveMessage = null;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var persona = await db.Personas.FindAsync(SelectedPersona.PersonaId);
            if (persona == null) throw new InvalidOperationException("페르소나를 찾을 수 없습니다.");

            persona.PrimaryModel = EditPrimaryModel.Trim();
            persona.FallbackModel = string.IsNullOrWhiteSpace(EditFallbackModel) ? null : EditFallbackModel.Trim();
            persona.Temperature = Math.Clamp(EditTemperature, 0f, 1f);
            persona.MaxTokens = Math.Clamp(EditMaxTokens, 256, 32768);
            persona.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            _configLoader.InvalidateAll();
            SaveMessage = "저장됨";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.InnerException?.Message ?? ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }
}

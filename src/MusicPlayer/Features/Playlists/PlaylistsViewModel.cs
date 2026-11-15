using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using MusicPlayer.Core.Playlists;
using MusicPlayer.Core.Primitives;
using MusicPlayer.Features.Player;
using MusicPlayer.Features.Playlists.Data;
using MusicPlayer.Playback;

namespace MusicPlayer.Features.Playlists;

public sealed partial class PlaylistsViewModel : ObservableObject, IDisposable
{
    private static readonly ImageSource DefaultCoverImage = ImageSource.FromFile("playlist_cover_placeholder.png");

    private readonly IPlaylistRepository _repository;
    private readonly PlaylistCoverStorage _coverStorage;
    private readonly IPlaybackQueue _playbackQueue;
    private readonly IAudioPlayer _audioPlayer;
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    private PlaylistItemViewModel? _editingPlaylist;
    private string? _unsavedPickedCoverPath;
    private int _suppressReloadCount;
    private int _reloadRequested;
    private bool _isDisposed;

    public PlayerViewModel Player { get; }

    public ObservableCollection<PlaylistItemViewModel> Playlists { get; } = [];

    [ObservableProperty]
    private bool _isEditorModalVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDeleteEditingPlaylist))]
    private bool _isEditMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDefaultActionRowVisible))]
    private bool _isDeleteConfirmationVisible;

    [ObservableProperty]
    private string _modalTitle = "Новый плейлист";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveModal))]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomCover))]
    [NotifyPropertyChangedFor(nameof(HasNoCustomCover))]
    private string? _editorCoverPath;

    [ObservableProperty]
    private ImageSource _editorCoverPreview = DefaultCoverImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveModal))]
    private bool _isBusy;

    public bool CanSaveModal => !string.IsNullOrWhiteSpace(EditorTitle) && !IsBusy;

    public bool HasCustomCover => !string.IsNullOrWhiteSpace(EditorCoverPath) && File.Exists(EditorCoverPath);

    public bool HasNoCustomCover => !HasCustomCover;

    public bool CanDeleteEditingPlaylist => IsEditMode && _editingPlaylist is { IsSystem: false };

    public bool IsDefaultActionRowVisible => !IsDeleteConfirmationVisible;

    public PlaylistsViewModel(
        IPlaylistRepository repository,
        PlaylistCoverStorage coverStorage,
        PlayerViewModel playerViewModel,
        IPlaybackQueue playbackQueue,
        IAudioPlayer audioPlayer)
    {
        _repository = repository;
        _coverStorage = coverStorage;
        Player = playerViewModel;
        _playbackQueue = playbackQueue;
        _audioPlayer = audioPlayer;

        _repository.PlaylistsChanged += OnRepositoryPlaylistsChanged;
    }

    public void LoadPlaylists()
    {
        _ = LoadPlaylistsAsync();
    }

    public async Task LoadPlaylistsAsync()
    {
        Player.Tick();

        Interlocked.Exchange(ref _reloadRequested, 1);
        if (!await _loadGate.WaitAsync(0))
            return;

        try
        {
            while (Interlocked.Exchange(ref _reloadRequested, 0) == 1)
            {
                Result<IReadOnlyList<Playlist>> result = await _repository.GetAllPlaylistsAsync();
                if (!result.TryGetOk(out IReadOnlyList<Playlist>? loaded))
                {
                    await ShowToastSafeAsync(result.Error.Message);
                    continue;
                }

                RunOnMainThread(() => SyncPlaylistCollection(loaded));
            }
        }
        finally
        {
            _loadGate.Release();
        }
    }

    [RelayCommand]
    private void OpenCreateModal()
    {
        CleanupUnsavedPickedCover();

        _editingPlaylist = null;
        IsEditMode = false;
        IsDeleteConfirmationVisible = false;
        ModalTitle = "Новый плейлист";
        EditorTitle = string.Empty;
        EditorCoverPath = null;
        EditorCoverPreview = DefaultCoverImage;
        IsEditorModalVisible = true;
    }

    [RelayCommand]
    private void OpenEditModal(PlaylistItemViewModel? item)
    {
        if (item is null || item.IsSystem)
            return;

        CleanupUnsavedPickedCover();

        _editingPlaylist = item;
        IsEditMode = true;
        IsDeleteConfirmationVisible = false;
        ModalTitle = "Изменить плейлист";
        EditorTitle = item.Title;
        EditorCoverPath = item.Playlist.CoverPath.TryGetSome(out string? path) ? path : null;
        EditorCoverPreview = item.CoverSource;
        IsEditorModalVisible = true;
    }

    [RelayCommand]
    private void CloseEditorModal()
    {
        CleanupUnsavedPickedCover();
        _editingPlaylist = null;
        IsDeleteConfirmationVisible = false;
        IsEditorModalVisible = false;
    }

    [RelayCommand]
    private void RequestDeletePlaylist()
    {
        if (!CanDeleteEditingPlaylist || IsBusy)
            return;

        IsDeleteConfirmationVisible = true;
    }

    [RelayCommand]
    private void CancelDeletePlaylist()
    {
        IsDeleteConfirmationVisible = false;
    }

    [RelayCommand]
    private async Task ConfirmDeletePlaylistAsync()
    {
        if (!CanDeleteEditingPlaylist || _editingPlaylist is null || IsBusy)
            return;

        IsBusy = true;
        Interlocked.Increment(ref _suppressReloadCount);
        try
        {
            PlaylistItemViewModel target = _editingPlaylist;
            Result<Unit> deleteResult = await _repository.DeletePlaylistAsync(target.Playlist);

            if (deleteResult.IsError)
            {
                IsDeleteConfirmationVisible = false;
                await ShowToastSafeAsync(deleteResult.Error.Message);
                return;
            }

            CleanupUnsavedPickedCover();
            Playlists.Remove(target);
            _editingPlaylist = null;
            IsDeleteConfirmationVisible = false;
            IsEditorModalVisible = false;

            await ShowToastSafeAsync("Удалено");
        }
        finally
        {
            Interlocked.Decrement(ref _suppressReloadCount);
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PickCoverPhotoAsync()
    {
        if (IsBusy)
            return;

        try
        {
            var options = new MediaPickerOptions
            {
                SelectionLimit = 1,
                Title = "Выберите обложку"
            };

            List<FileResult> picked = await MediaPicker.Default.PickPhotosAsync(options);
            FileResult? photo = picked.Count > 0 ? picked[0] : null;
            if (photo is null)
                return;

            Result<string> saveResult = await _coverStorage.SaveCoverAsync(photo);
            if (!saveResult.TryGetOk(out string? savedPath))
            {
                await ShowToastSafeAsync(saveResult.Error.Message);
                return;
            }

            CleanupUnsavedPickedCover();
            _unsavedPickedCoverPath = savedPath;
            EditorCoverPath = savedPath;
            EditorCoverPreview = ImageSource.FromFile(savedPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaylistsViewModel] PickCoverPhotoAsync failed: {ex.Message}");
            await ShowToastSafeAsync("Не удалось выбрать фото");
        }
    }

    [RelayCommand]
    private void RemoveCoverPhoto()
    {
        if (IsBusy)
            return;

        CleanupUnsavedPickedCover();
        EditorCoverPath = null;
        EditorCoverPreview = DefaultCoverImage;
    }

    [RelayCommand]
    private async Task SavePlaylistAsync()
    {
        if (!CanSaveModal)
            return;

        IsBusy = true;
        Interlocked.Increment(ref _suppressReloadCount);
        try
        {
            Option<string> coverOption = !string.IsNullOrWhiteSpace(EditorCoverPath) && File.Exists(EditorCoverPath)
                ? Option.Some(EditorCoverPath)
                : Option.None;

            if (IsEditMode && _editingPlaylist is not null)
            {
                Result<Playlist> updateResult = await _repository.UpdatePlaylistAsync(
                    _editingPlaylist.Playlist,
                    EditorTitle,
                    coverOption);

                if (!updateResult.TryGetOk(out _))
                {
                    await ShowToastSafeAsync(updateResult.Error.Message);
                    return;
                }

                _unsavedPickedCoverPath = null;
                _editingPlaylist.Refresh();
                _editingPlaylist = null;
                IsDeleteConfirmationVisible = false;
                IsEditorModalVisible = false;

                await ShowToastSafeAsync("Сохранено");
            }
            else
            {
                Result<Playlist> createResult = await _repository.CreatePlaylistAsync(
                    EditorTitle,
                    coverOption);

                if (!createResult.TryGetOk(out Playlist? created))
                {
                    await ShowToastSafeAsync(createResult.Error.Message);
                    return;
                }

                _unsavedPickedCoverPath = null;
                Playlists.Add(new PlaylistItemViewModel(created));
                IsDeleteConfirmationVisible = false;
                IsEditorModalVisible = false;

                await ShowToastSafeAsync("Плейлист создан");
            }
        }
        finally
        {
            Interlocked.Decrement(ref _suppressReloadCount);
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenPlaylistAsync(PlaylistItemViewModel? item)
    {
        if (item is null)
            return;

        await Shell.Current.GoToAsync($"{nameof(PlaylistDetailPage)}?playlistId={item.Id:N}");
    }

    private void SyncPlaylistCollection(IReadOnlyList<Playlist> loaded)
    {
        if (Playlists.Count == 0)
        {
            foreach (Playlist playlist in loaded)
            {
                Playlists.Add(new PlaylistItemViewModel(playlist));
            }
            return;
        }

        var loadedById = new Dictionary<Guid, Playlist>(loaded.Count);
        foreach (Playlist playlist in loaded)
        {
            loadedById[playlist.Id] = playlist;
        }

        for (int i = Playlists.Count - 1; i >= 0; i--)
        {
            if (!loadedById.ContainsKey(Playlists[i].Id))
            {
                Playlists.RemoveAt(i);
            }
        }

        for (int targetIndex = 0; targetIndex < loaded.Count; targetIndex++)
        {
            Playlist target = loaded[targetIndex];
            int existingIndex = FindPlaylistIndex(target.Id);

            if (existingIndex < 0)
            {
                Playlists.Insert(targetIndex, new PlaylistItemViewModel(target));
            }
            else
            {
                if (existingIndex != targetIndex)
                {
                    Playlists.Move(existingIndex, targetIndex);
                }

                PlaylistItemViewModel existingItem = Playlists[targetIndex];
                if (existingItem.Title == target.Title && existingItem.Playlist.CoverPath == target.CoverPath)
                {
                    existingItem.Playlist.ReplaceTracks(target.Tracks);
                    existingItem.Refresh();
                }
                else
                {
                    Playlists[targetIndex] = new PlaylistItemViewModel(target);
                }
            }
        }
    }

    private int FindPlaylistIndex(Guid id)
    {
        for (int i = 0; i < Playlists.Count; i++)
        {
            if (Playlists[i].Id == id)
                return i;
        }

        return -1;
    }

    private void OnRepositoryPlaylistsChanged(object? sender, EventArgs e)
    {
        if (Volatile.Read(ref _suppressReloadCount) > 0)
            return;

        LoadPlaylists();
    }

    private void CleanupUnsavedPickedCover()
    {
        if (string.IsNullOrWhiteSpace(_unsavedPickedCoverPath))
            return;

        _coverStorage.DeleteCoverIfExists(_unsavedPickedCoverPath);
        _unsavedPickedCoverPath = null;
    }

    private static void RunOnMainThread(Action action)
    {
        if (MainThread.IsMainThread)
        {
            action();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(action);
        }
    }

    private static async Task ShowToastSafeAsync(string message)
    {
        try
        {
            var toast = Toast.Make(message, ToastDuration.Short, 14d);
            await toast.Show(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaylistsViewModel] Toast notification skipped: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _repository.PlaylistsChanged -= OnRepositoryPlaylistsChanged;
        CleanupUnsavedPickedCover();
        _loadGate.Dispose();
    }
}

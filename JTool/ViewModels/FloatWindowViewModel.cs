using GongSolutions.Wpf.DragDrop;
using JTool.Models;
using JTool.Services;

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace JTool.ViewModels;

public class FloatWindowViewModel : ObservableObject, IDropTarget
{
    private readonly ConfigService _configService;
    private readonly IconService _iconService;
    private readonly FileOperationService _fileService;
    private readonly WebImageService _webImageService;
    private readonly BoardService _boardService;

    private LauncherConfig _config;

    public ObservableCollection<ShortcutItemViewModel> Shortcuts { get; } = new();
    public ObservableCollection<string> TargetDirs { get; } = new();
    public ObservableCollection<string> Groups { get; } = new();

    public ObservableCollection<BoardItemViewModel> BoardItems { get; } = new();
    public ObservableCollection<BoardItemViewModel> BoardTextItems { get; } = new();
    public ObservableCollection<BoardItemViewModel> BoardImageItems { get; } = new();

    // ===== 布局 =====
    private double _panelWidth;
    public double PanelWidth
    {
        get => _panelWidth;
        set => SetProperty(ref _panelWidth, Math.Max(160, Math.Min(800, value)));
    }
    private double _panelHeight;
    public double PanelHeight
    {
        get => _panelHeight;
        set => SetProperty(ref _panelHeight, Math.Max(120, Math.Min(900, value)));
    }
    private double _cellWidth;
    public double CellWidth { get => _cellWidth; set => SetProperty(ref _cellWidth, value); }
    private double _cellHeight;
    public double CellHeight { get => _cellHeight; set => SetProperty(ref _cellHeight, value); }
    private double _iconSize;
    public double IconSize { get => _iconSize; set => SetProperty(ref _iconSize, value); }

    // ===== 状态 =====
    private bool _isBallVisible = true;
    public bool IsBallVisible { get => _isBallVisible; set => SetProperty(ref _isBallVisible, value); }
    private bool _isReordering;
    public bool IsReordering { get => _isReordering; private set => SetProperty(ref _isReordering, value); }
    private bool _isBoardExpanded = true;
    public bool IsBoardExpanded { get => _isBoardExpanded; set => SetProperty(ref _isBoardExpanded, value); }

    public bool HasBoardItems => BoardItems.Count > 0;
    public bool HasBoardText => BoardTextItems.Count > 0;
    public bool HasBoardImage => BoardImageItems.Count > 0;

    // ===== 命令 =====
    public RelayCommand LaunchCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand ExitCommand { get; }
    public RelayCommand ToggleVisibilityCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand AddGroupCommand { get; }
    public RelayCommand CopyBoardItemCommand { get; }
    public RelayCommand RemoveBoardItemCommand { get; }
    public RelayCommand ToggleBoardCommand { get; }

    private readonly DefaultDropHandler _defaultDropHandler = new();

    public FloatWindowViewModel(ConfigService configService, IconService iconService,
        FileOperationService fileService, WebImageService webImageService, BoardService boardService)
    {
        _configService = configService;
        _iconService = iconService;
        _fileService = fileService;
        _webImageService = webImageService;
        _boardService = boardService;

        _config = _configService.Load();

        LaunchCommand = new RelayCommand(p => Launch(p as string));
        RemoveCommand = new RelayCommand(p => Remove(p as ShortcutItemViewModel));
        ExitCommand = new RelayCommand(_ => Application.Current.Shutdown());
        ToggleVisibilityCommand = new RelayCommand(_ => IsBallVisible = !IsBallVisible);
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
        MoveUpCommand = new RelayCommand(p => MoveItem(p as ShortcutItemViewModel, -1));
        MoveDownCommand = new RelayCommand(p => MoveItem(p as ShortcutItemViewModel, +1));
        AddGroupCommand = new RelayCommand(p => AddGroup(p as string));
        CopyBoardItemCommand = new RelayCommand(p => CopyBoardItem(p as BoardItemViewModel));
        RemoveBoardItemCommand = new RelayCommand(p => RemoveBoardItem(p as BoardItemViewModel));
        ToggleBoardCommand = new RelayCommand(_ => IsBoardExpanded = !IsBoardExpanded);

        LoadFromConfig();
        RefreshGroups();
    }

    public double WindowLeft
    {
        get => _config.WindowLeft;
        set { _config.WindowLeft = value; OnPropertyChanged(); }
    }
    public double WindowTop
    {
        get => _config.WindowTop;
        set { _config.WindowTop = value; OnPropertyChanged(); }
    }

    private void LoadFromConfig()
    {
        Shortcuts.Clear();
        foreach (var s in _config.Shortcuts)
            Shortcuts.Add(new ShortcutItemViewModel(s, _iconService, NotifyItemChanged));

        TargetDirs.Clear();
        foreach (var d in _config.TargetDirs.Where(Directory.Exists).Distinct())
            TargetDirs.Add(d);

        PanelWidth = _config.PanelWidth > 0 ? _config.PanelWidth : 260;
        PanelHeight = _config.PanelHeight > 0 ? _config.PanelHeight : 360;
        CellWidth = _config.Grid.CellWidth;
        CellHeight = _config.Grid.CellHeight;
        IconSize = _config.Grid.IconSize;

        // 看板（独立持久化）
        BoardItems.Clear();
        foreach (var b in _boardService.Load())
            BoardItems.Add(new BoardItemViewModel(b));
        RebuildBoardCategories();
    }

    // ===== 快捷项 =====
    private void Launch(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启动失败：{ex.Message}", "JTool", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Remove(ShortcutItemViewModel? vm)
    {
        if (vm == null) return;
        Shortcuts.Remove(vm);
        _config.Shortcuts.Remove(vm.Model);
        _iconService.Invalidate(vm.Path);
        RefreshGroups();
        Save();
    }

    public void AddShortcuts(string[] paths)
    {
        foreach (var path in paths)
        {
            if (_config.Shortcuts.Any(s => s.Path == path)) continue;
            string name = File.Exists(path) ? Path.GetFileNameWithoutExtension(path) : SafeDirName(path);
            var model = new ShortcutItem { Name = name, Path = path };
            _config.Shortcuts.Add(model);
            Shortcuts.Add(new ShortcutItemViewModel(model, _iconService, NotifyItemChanged));
        }
        RefreshGroups();
        Save();
    }

    public void AddTargetDirs(string[] paths)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path) && !_config.TargetDirs.Contains(path))
            {
                _config.TargetDirs.Add(path);
                TargetDirs.Add(path);
            }
        }
        Save();
    }

    public void MoveToDir(string[] paths, string targetDir) => _fileService.MoveToDirectory(paths, targetDir);

    public Task<int> SaveWebImageAsync(IDataObject data, string targetDir)
        => _webImageService.SaveDroppedImageAsync(data, targetDir);

    public void SaveWindowPosition(double left, double top)
    {
        _config.WindowLeft = left;
        _config.WindowTop = top;
        Save();
    }

    public void SavePanelSize(double width, double height)
    {
        PanelWidth = width;     // 走属性夹取
        PanelHeight = height;
        _config.PanelWidth = PanelWidth;
        _config.PanelHeight = PanelHeight;
        Save();
    }

    // ===== 设置 =====
    private Views.SettingsWindow? _settingsWindow;
    private void OpenSettings()
    {
        if (_settingsWindow != null) { _settingsWindow.Activate(); return; }
        _settingsWindow = new Views.SettingsWindow { DataContext = this };
        _settingsWindow.Closed += (s, e) => { _settingsWindow = null; Save(); };
        _settingsWindow.Show();
    }

    private void MoveItem(ShortcutItemViewModel? vm, int delta)
    {
        if (vm == null) return;
        int idx = Shortcuts.IndexOf(vm);
        int target = idx + delta;
        if (idx < 0 || target < 0 || target >= Shortcuts.Count) return;
        Shortcuts.Move(idx, target);
        SyncOrderAndSave();
    }

    // ===== 分组 =====
    public void RefreshGroups()
    {
        Groups.Clear();
        Groups.Add("默认");
        foreach (var g in Shortcuts.Select(s => s.Group)
                     .Where(g => !string.IsNullOrWhiteSpace(g) && g != "默认").Distinct())
            Groups.Add(g);
    }

    private void AddGroup(string? name)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || Groups.Contains(name)) return;
        Groups.Add(name);
    }

    public void NotifyItemChanged()
    {
        RefreshGroups();
        Save();
    }

    // ===== 看板 =====
    public void AddTextToBoard(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var model = _boardService.CreateTextItem(text);
        BoardItems.Add(new BoardItemViewModel(model));
        RebuildBoardCategories();
        SaveBoard();
    }

    public void AddImageToBoard(BitmapSource bmp)
    {
        var model = _boardService.CreateImageItem(bmp);
        if (model == null) return;
        BoardItems.Add(new BoardItemViewModel(model));
        RebuildBoardCategories();
        SaveBoard();
    }

    /// <summary>URL 图片：先插占位项立即反馈，后台下载完成再原地替换。成功返回 true，失败返回 false。</summary>
    public async Task<bool> AddImageFromUrlAsync(string url)
    {
        var placeholder = new BoardItemViewModel(new BoardItem
        {
            Type = BoardItemType.Image,
            CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
        })
        { LoadState = BoardLoadState.Loading };

        BoardItems.Add(placeholder);
        RebuildBoardCategories();

        try
        {
            var bmp = await _webImageService.DownloadBitmapAsync(url);
            var model = _boardService.CreateImageItem(bmp);
            if (model == null) throw new Exception("图片保存失败");
            placeholder.SetImageReady(model.ImagePath);
            SaveBoard();
            return true;
        }
        catch
        {
            BoardItems.Remove(placeholder);
            RebuildBoardCategories();
            return false;
        }
    }

    private void CopyBoardItem(BoardItemViewModel? vm)
    {
        if (vm == null) return;
        if (vm.IsText) _boardService.CopyText(vm.Text);
        else if (vm.IsImageReady) _boardService.CopyImage(vm.ImagePath);
    }

    private void RemoveBoardItem(BoardItemViewModel? vm)
    {
        if (vm == null) return;
        BoardItems.Remove(vm);
        _boardService.DeleteItemFile(vm.Model);
        RebuildBoardCategories();
        SaveBoard();
    }

    private void RebuildBoardCategories()
    {
        BoardTextItems.Clear();
        BoardImageItems.Clear();
        foreach (var b in BoardItems)
        {
            if (b.IsText) BoardTextItems.Add(b);
            else BoardImageItems.Add(b);
        }
        OnPropertyChanged(nameof(HasBoardItems));
        OnPropertyChanged(nameof(HasBoardText));
        OnPropertyChanged(nameof(HasBoardImage));
    }

    private void SaveBoard()
        => _boardService.Save(BoardItems
            .Where(v => !v.IsLoading && !v.IsFailed)
            .Select(v => v.Model));

    // ===== 拖拽排序 =====
    public void DragOver(IDropInfo dropInfo)
    {
        IsReordering = true;
        _defaultDropHandler.DragOver(dropInfo);
    }

    public void Drop(IDropInfo dropInfo)
    {
        _defaultDropHandler.Drop(dropInfo);
        SyncOrderAndSave();
        IsReordering = false;
    }

    public void ResetReordering() => IsReordering = false;

    private void SyncOrderAndSave()
    {
        _config.Shortcuts.Clear();
        foreach (var s in Shortcuts) _config.Shortcuts.Add(s.Model);
        Save();
    }

    public void Save() => _configService.Save(_config);

    private static string SafeDirName(string dir)
    {
        try { return new DirectoryInfo(dir).Name; } catch { return dir; }
    }
}

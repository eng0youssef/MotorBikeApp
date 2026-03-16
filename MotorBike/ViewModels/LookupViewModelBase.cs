using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;

namespace MotorBike.ViewModels;

/// <summary>
/// Base ViewModel for standard lookup/CRUD screens.
/// Provides DataGrid binding, form binding, and Add/Edit/Save/Delete commands.
/// </summary>
public abstract partial class LookupViewModelBase<T> : ObservableObject where T : class, new()
{
    protected readonly IRepository<T> _repository;

    /// <summary>Tracks whether the current form operation is an insert (true) or update (false).</summary>
    private bool _isInsertMode;

    [ObservableProperty]
    private ObservableCollection<T> _items = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private T? _selectedItem;

    [ObservableProperty]
    private T _formItem = new();

    partial void OnFormItemChanged(T value) => OnFormItemChangedHook(value);

    /// <summary>Hook for subclasses to react when FormItem is replaced.</summary>
    protected virtual void OnFormItemChangedHook(T value) { }

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string? _statusMessage;

    protected LookupViewModelBase(IRepository<T> repository)
    {
        _repository = repository;
    }

    // ── Commands ─────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            var data = await _repository.GetAllAsync();
            Items = new ObservableCollection<T>(data);
            StatusMessage = $"تم تحميل {Items.Count} سجل";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في التحميل: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task AddNewAsync()
    {
        try
        {
            var item = new T();
            SetDefaultValues(item);
            await SetNewIdAsync(item);
            _isInsertMode = true;
            IsEditing = true;
            SelectedItem = null;
            FormItem = item; // assign LAST so UI sees fully initialized object
            StatusMessage = "سجل جديد — أدخل البيانات ثم اضغط حفظ";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في إنشاء سجل جديد: {ex.Message}";
        }
    }

    [RelayCommand]
    public void EditSelected()
    {
        if (SelectedItem is null) return;
        FormItem = CloneEntity(SelectedItem);
        _isInsertMode = false;
        IsEditing = true;
        StatusMessage = "تعديل السجل — غيّر البيانات ثم اضغط حفظ";
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (FormItem is null) return;

        try
        {
            SetAuditFields(FormItem, _isInsertMode);

            if (_isInsertMode)
            {
                await _repository.InsertAsync(FormItem);
                StatusMessage = "تم إضافة السجل بنجاح ✓";
            }
            else
            {
                await _repository.UpdateAsync(FormItem);
                StatusMessage = "تم تعديل السجل بنجاح ✓";
            }

            _isInsertMode = false;
            IsEditing = false;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في الحفظ: {ex.InnerException?.Message ?? ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (SelectedItem is null) return;

        try
        {
            var id = GetEntityId(SelectedItem);
            await _repository.DeleteAsync(id);
            StatusMessage = "تم حذف السجل ✓";
            IsEditing = false;
            FormItem = new T();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في الحذف: {ex.Message}";
        }
    }

    [RelayCommand]
    public void CancelEdit()
    {
        _isInsertMode = false;
        IsEditing = false;
        FormItem = new T();
        StatusMessage = null;
    }

    // ── Abstract / Virtual hooks for subclasses ──────────────────────

    /// <summary>Returns the primary key value from the entity.</summary>
    protected abstract object GetEntityId(T entity);

    /// <summary>Determines if this is a new (un-saved) record.</summary>
    protected abstract bool IsNewRecord(T entity);

    /// <summary>Sets the next available ID on a new entity.</summary>
    protected virtual async Task SetNewIdAsync(T entity)
    {
        var nextId = await _repository.GetNextIdAsync();
        SetEntityId(entity, nextId);
    }

    /// <summary>Sets the PK value on the entity.</summary>
    protected abstract void SetEntityId(T entity, int id);

    /// <summary>Creates a shallow copy for form editing.</summary>
    protected virtual T CloneEntity(T source)
    {
        var clone = new T();
        foreach (var prop in typeof(T).GetProperties()
            .Where(p => p.CanWrite && p.CanRead))
        {
            prop.SetValue(clone, prop.GetValue(source));
        }
        return clone;
    }

    /// <summary>Sets default values for a new entity (e.g. Active = true, DateTime fields = now).</summary>
    protected virtual void SetDefaultValues(T entity)
    {
        var activeProp = typeof(T).GetProperty("Active");
        activeProp?.SetValue(entity, true);

        // Pre-fill ALL DateTime properties with DateTime.Now to prevent SqlDateTime overflow.
        // This ensures that non-nullable DateTime fields never stay at 0001-01-01.
        // Pre-fill non-nullable DateTime properties to prevent SqlDateTime overflow.
        // Only fills properties that are still at default (0001-01-01).
        // Nullable DateTime? properties are left as null.
        var now = DateTime.Now;
        foreach (var prop in typeof(T).GetProperties())
        {
            if (prop.PropertyType == typeof(DateTime) && prop.CanWrite)
            {
                var current = (DateTime)prop.GetValue(entity)!;
                if (current == default)
                    prop.SetValue(entity, now);
            }
        }
    }

    /// <summary>
    /// Sets audit fields. On INSERT: sets Add* fields only.
    /// On UPDATE: sets Edit* fields only.
    /// Uses AppSession.CurrentUserId for the logged-in user's ID (null if not logged in).
    /// </summary>
    protected virtual void SetAuditFields(T entity, bool isInsert)
    {
        var now = DateTime.Now;
        var pc = Environment.MachineName;
        var userId = AppSession.CurrentUserId;

        if (isInsert)
        {
            // AddUser is now int? — null if no user logged in
            typeof(T).GetProperty("AddUser")?.SetValue(entity, userId);
            typeof(T).GetProperty("AddDate")?.SetValue(entity, now);
            typeof(T).GetProperty("AddPc")?.SetValue(entity, pc);
            // Leave EditUser, EditDate, EditPc as null on insert
        }
        else
        {
            // EditUser is int? (nullable) — null is fine
            typeof(T).GetProperty("EditUser")?.SetValue(entity, userId);
            typeof(T).GetProperty("EditDate")?.SetValue(entity, now);
            typeof(T).GetProperty("EditPc")?.SetValue(entity, pc);
        }
    }

    partial void OnSelectedItemChanged(T? value)
    {
        if (value is not null && !IsEditing)
        {
            FormItem = CloneEntity(value);
        }
    }
}

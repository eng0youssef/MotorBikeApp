using System.Collections.ObjectModel;
using System.Linq;
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

    /// <summary>Exposes insert mode flag to subclasses and views.</summary>
    public bool IsInsertMode => _isInsertMode;

    [ObservableProperty]
    private ObservableCollection<T> _items = [];

    /// <summary>Search/filter text - filters the grid in real-time.</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value) => RefreshFilteredItems();

    partial void OnItemsChanged(ObservableCollection<T> value) => RefreshFilteredItems();

    /// <summary>Filtered collection bound to the DataGrid.</summary>
    [ObservableProperty]
    private ObservableCollection<T> _filteredItems = [];

    /// <summary>Filters Items based on SearchText across all string properties.</summary>
    protected virtual void RefreshFilteredItems()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredItems = new ObservableCollection<T>(Items);
        }
        else
        {
            var lower = SearchText.Trim().ToLower();

            // String properties
            var stringProps = typeof(T).GetProperties()
                .Where(p => p.CanRead && p.PropertyType == typeof(string))
                .ToArray();

            // Numeric properties (int, long, decimal, double, float) → converted to string for search
            var numericTypes = new HashSet<Type>
            {
                typeof(int), typeof(long), typeof(decimal),
                typeof(double), typeof(float),
                typeof(int?), typeof(long?), typeof(decimal?),
                typeof(double?), typeof(float?)
            };
            var numericProps = typeof(T).GetProperties()
                .Where(p => p.CanRead && numericTypes.Contains(p.PropertyType))
                .ToArray();

            var filtered = Items.Where(item =>
                stringProps.Any(p =>
                {
                    var val = p.GetValue(item) as string;
                    return val != null && val.ToLower().Contains(lower);
                })
                ||
                numericProps.Any(p =>
                {
                    var val = p.GetValue(item);
                    return val != null && val.ToString()!.Contains(lower);
                }));

            FilteredItems = new ObservableCollection<T>(filtered);
        }
    }

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
            
            // Auto-activate "Add New" mode as requested by user
            await AddNewAsync();
            
            StatusMessage = $"تم تحميل {Items.Count} سجل";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في التحميل: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    public async Task AddNewAsync()
    {
        try
        {
            var item = new T();
            SetDefaultValues(item);
            // Delaying ID generation until SaveAsync: await SetNewIdAsync(item);
            await Task.CompletedTask;

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
    public virtual async Task SaveAsync()
    {
        if (FormItem is null) return;

        if (System.Enum.TryParse<MotorBike.Models.ScreenId>(this.GetType().Name.Replace("ViewModel", ""), out var screenId))
        {
            var requiredAbility = _isInsertMode ? MotorBike.Models.AppAbility.Add : MotorBike.Models.AppAbility.Edit;
            if (!AppSession.HasPermission(screenId, requiredAbility))
            {
                System.Windows.MessageBox.Show("عفواً، ليس لديك صلاحية لإجراء هذه العملية.", "صلاحيات غير كافية", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Stop);
                return;
            }
        }

        try
        {
            if (_isInsertMode)
            {
                await SetNewIdAsync(FormItem);
            }

            var wasInsert = _isInsertMode;
            await BeforeSaveAsync(wasInsert);

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

            await AfterSaveAsync(wasInsert);

            _isInsertMode = false;
            // Retain IsEditing = true so the user can continue editing if they wish
            
            // Reload grid data
            var data = await _repository.GetAllAsync();
            Items = new ObservableCollection<T>(data);

            // Re-select the saved item to reflect the updated data
            var savedId = GetEntityId(FormItem);
            var savedItemConfig = Items.FirstOrDefault(i => GetEntityId(i).Equals(savedId));
            
            if (savedItemConfig != null)
            {
                SelectedItem = savedItemConfig;
                FormItem = CloneEntity(savedItemConfig);
                IsEditing = true;
            }

            StatusMessage = wasInsert ? "تم إضافة السجل بنجاح ✓ " : "تم تعديل السجل بنجاح ✓";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في الحفظ: {ex.InnerException?.Message ?? ex.Message}";
        }
    }

    [RelayCommand]
    public virtual async Task DeleteAsync()
    {
        if (SelectedItem is null) return;

        if (System.Enum.TryParse<MotorBike.Models.ScreenId>(this.GetType().Name.Replace("ViewModel", ""), out var screenId))
        {
            if (!AppSession.HasPermission(screenId, MotorBike.Models.AppAbility.Delete))
            {
                System.Windows.MessageBox.Show("عفواً، ليس لديك صلاحية للحذف.", "صلاحيات غير كافية", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Stop);
                return;
            }
        }

        var res = System.Windows.MessageBox.Show("هل أنت متأكد من الحذف نهائياً؟", "تأكيد الحذف", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (res != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            await BeforeDeleteAsync();

            var id = GetEntityId(SelectedItem);
            await _repository.DeleteAsync(id);

            await AfterDeleteAsync();

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

    // ── Balance recalculation hooks ──────────────────────────────────

    /// <summary>Called before saving. Use to capture old values (e.g. old CustomerId, old CashId) for balance recalculation.</summary>
    protected virtual Task BeforeSaveAsync(bool isInsert) => Task.CompletedTask;

    /// <summary>Called after saving. Use to recalculate balances for both old and new entities.</summary>
    protected virtual Task AfterSaveAsync(bool wasInsert) => Task.CompletedTask;

    /// <summary>Called before deleting. Use to capture entity values needed for balance recalculation.</summary>
    protected virtual Task BeforeDeleteAsync() => Task.CompletedTask;

    /// <summary>Called after deleting. Use to recalculate balances for affected entities.</summary>
    protected virtual Task AfterDeleteAsync() => Task.CompletedTask;

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
        if (value is not null)
        {
            FormItem = CloneEntity(value);
            _isInsertMode = false;
            IsEditing = false;
        }
    }
}

using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MotorBike.Models;

public partial class InspectionSub : ObservableObject
{
    private int _id;
    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private int _inspId;
    public int InspId
    {
        get => _inspId;
        set => SetProperty(ref _inspId, value);
    }

    private string _itemName = string.Empty;
    public string ItemName
    {
        get => _itemName;
        set => SetProperty(ref _itemName, value);
    }

    private bool _status = true; // Default to 'OK' (سليم)
    public bool Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    private string? _note;
    public string? Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public virtual Inspection Inspection { get; set; } = null!;
}

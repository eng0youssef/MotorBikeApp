using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MotorBike.Models;

public partial class SalesMaintenance : ObservableObject
{
    private int _id;
    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private int _salesId;
    public int SalesId
    {
        get => _salesId;
        set => SetProperty(ref _salesId, value);
    }

    private string _itemName = string.Empty;
    public string ItemName
    {
        get => _itemName;
        set => SetProperty(ref _itemName, value);
    }

    private double _cost;
    public double Cost
    {
        get => _cost;
        set
        {
            if (SetProperty(ref _cost, value))
            {
                OnPropertyChanged(nameof(Profit));
            }
        }
    }

    private double _price;
    public double Price
    {
        get => _price;
        set
        {
            if (SetProperty(ref _price, value))
            {
                OnPropertyChanged(nameof(Profit));
            }
        }
    }

    private bool _isCash;
    public bool IsCash
    {
        get => _isCash;
        set => SetProperty(ref _isCash, value);
    }

    private int? _cashId;
    public int? CashId
    {
        get => _cashId;
        set => SetProperty(ref _cashId, value);
    }

    private int? _suppId;
    public int? SuppId
    {
        get => _suppId;
        set => SetProperty(ref _suppId, value);
    }

    [ObservableProperty] private string _cashName = string.Empty;
    [ObservableProperty] private string _suppName = string.Empty;

    public double Profit => Price - Cost;
}

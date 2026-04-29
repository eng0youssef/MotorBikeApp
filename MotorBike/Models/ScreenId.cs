namespace MotorBike.Models;

/// <summary>
/// يمثل معرفات الشاشات (FrmID) المستخدمة في جدول User_Sub للتحقق من الصلاحيات.
/// </summary>
public enum ScreenId
{
    // --- البيانات الأساسية ---
    Company = 1,
    Users = 2,
    Cities = 3,
    Customers = 4,
    Suppliers = 5,
    Units = 6,
    ItemCategories = 7,
    Items = 8,
    Stores = 9,
    OpenStock = 10,
    CarBrands = 11,
    CarModels = 12,
    Colors = 13,
    Cash = 14,
    Omla = 15,
    ExpGroups = 16,
    Expenses = 17,

    // --- العمليات (الفواتير) ---
    Buys = 20,
    Sales = 21,
    BuyCar = 22,
    SalesCar = 23,

    // --- المرتجعات ---
    ReBuy = 30,
    ReSales = 31,

    // --- المدفوعات والتحويلات ---
    CusPayments = 40,
    SuppPayments = 41,
    ExpPayments = 42,
    CashTransfers = 43,

    // --- أخرى ---
    Cars = 50,
    Inspections = 51,

    // --- الاستيراد ---
    ImportSuppliers = 60,
    ImportExpenses = 61,
    ImportInvoice = 62,
    ImportPayments = 63,

    // --- التقارير ---
    StoreReports = 70,
    SalesReports = 71,
    PurchasesReports = 72,
    ProfitsReports = 73,
    CarsReports = 74,
    ImportReports = 75,
    CashReports = 76,

    // --- الإعدادات ---
    Settings = 80
}

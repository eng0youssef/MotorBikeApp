using System.Collections.Generic;

namespace MotorBike.Models;

/// <summary>سطر في كشف حساب تفصيلي للعميل</summary>
public class DetailedAccountRow
{
    public System.DateTime RawDate { get; set; } = System.DateTime.MinValue;
    public string Date     { get; set; } = "";
    public string RefNo    { get; set; } = "";
    public string Branch   { get; set; } = "";
    public string Agent    { get; set; } = "";
    public string TransType { get; set; } = "";
    public string Notes    { get; set; } = "";
    public double Debit    { get; set; }
    public double Credit   { get; set; }
    public double RunningDebit  { get; set; }
    public double RunningCredit { get; set; }

    public bool IsCarTransaction { get; set; }

    // تفاصيل الأصناف (فارغة للتحصيلات والرصيد السابق)
    public List<InvoiceSubItem> Items { get; set; } = [];
    public bool HasItems => Items.Count > 0;
}

/// <summary>صنف داخل فاتورة مبيعات أو مرتجع</summary>
public class InvoiceSubItem
{
    public string ItemName { get; set; } = "";
    public string Unit     { get; set; } = "";
    public double Qty      { get; set; }
    public double Price    { get; set; }
    public double DiscPer  { get; set; }
    public double Total    { get; set; }

    // حقول إضافية خاصة بالموتوسيكلات
    public string ChassisNo { get; set; } = "";
    public string MotorNo   { get; set; } = "";
    public string PlateNo   { get; set; } = "";
    public int Mileage       { get; set; }
}

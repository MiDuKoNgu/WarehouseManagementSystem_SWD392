using System.Globalization;
using System.IO.Compression;
using System.Xml;
using ClosedXML.Excel;
using Procurement.Models.PurchaseOrders;

namespace Procurement.Excel;

public sealed class WorkbookFormatException(string message) : Exception(message);

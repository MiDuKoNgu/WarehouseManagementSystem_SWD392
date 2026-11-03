extern alias InventoryService;
using System.Net;
using System.Net.Http.Json;
using Catalog.BLL.Products;
using Catalog.Models.Products;
using Inventory.DAL.Receiving;
using Inventory.Models.Receiving;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Catalog.Products.Tests;

public static partial class ProductTests
{
    private sealed class FakeProducts:IProductRepository
    {
        public ProductDetail? Product;public ProductQuery? LastQuery;private int _version;
        public Task<ProductPage> ListAsync(ProductQuery query,CancellationToken ct){LastQuery=query;return Task.FromResult(new ProductPage(Product is null?[]:[Product],Product is null?0:1,query.Page,query.PageSize));}
        public Task<ProductDetail?> FindAsync(int id,CancellationToken ct)=>Task.FromResult(id==1?Product:null);
        public Task<ProductOptions> OptionsAsync(CancellationToken ct)=>Task.FromResult(new ProductOptions([new(1,"Điều hòa")],[new(1,"Daikin")],[new(1,"Supplier")]));
        public Task<ProductDetail> SaveAsync(int? id,ProductInput input,int actor,byte[]? version,CancellationToken ct)
        {
            if(id is not null && Product is null)throw new ProductNotFoundException();
            if(id is not null && Product!.ProductCode!=input.ProductCode)throw new ProductValidationException(["Mã không được đổi."]);
            if(id is not null && Product!.RowVersion!=Convert.ToBase64String(version!))throw new ProductConflictException("Stale version");
            if(id is null && Product is not null)throw new ProductConflictException("Duplicate code");
            Product=new(1,input.ProductCode,input.ProductName,input.Model,input.Unit,1,"Điều hòa",1,"Daikin",input.Status,input.Specifications,input.LengthCm,input.WidthCm,input.HeightCm,input.GrossWeightKg,[new(1,"Supplier")],Product?.CreatedByUserId??actor,DateTime.UtcNow,null,Convert.ToBase64String(BitConverter.GetBytes((long)++_version)));
            return Task.FromResult(Product);
        }
    }
}

using System;
using System.Linq;
using MiniMart.Web.Models.Entities;

namespace MiniMart.Web.Data
{
    public static class DbInitializer
    {
        public static void Initialize(MiniMartDbContext context)
        {
            try
            {
                // Tự động tạo cấu trúc bảng nếu database chưa có
                context.Database.EnsureCreated();

                // 1. Cửa hàng mặc định
                if (!context.Stores.Any())
                {
                    var store = new Store
                    {
                        StoreCode = "ST001",
                        Name = "MiniMart Trụ Sở Chính",
                        Address = "123 Nguyễn Huệ, Quận 1, TP. Hồ Chí Minh",
                        Phone = "02838221144",
                        Email = "contact@minimart.vn",
                        StoreType = "MINI_MART",
                        Status = "ACTIVE",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    context.Stores.Add(store);
                    context.SaveChanges();
                }

                var defaultStore = context.Stores.First();

                // 2. Tài khoản nhân viên mặc định
                if (!context.Users.Any())
                {
                    context.Users.AddRange(
                        new User
                        {
                            Username = "admin",
                            Password = BCrypt.Net.BCrypt.HashPassword("admin123"),
                            FullName = "Quản Trị Viên",
                            Email = "admin@minimart.vn",
                            Role = "ADMIN",
                            StoreId = defaultStore.Id,
                            Status = "ACTIVE",
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        },
                        new User
                        {
                            Username = "manager",
                            Password = BCrypt.Net.BCrypt.HashPassword("123456"),
                            FullName = "Quản Lý Cửa Hàng",
                            Email = "manager@minimart.vn",
                            Role = "STORE_MANAGER",
                            StoreId = defaultStore.Id,
                            Status = "ACTIVE",
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        },
                        new User
                        {
                            Username = "cashier",
                            Password = BCrypt.Net.BCrypt.HashPassword("123456"),
                            FullName = "Thu Ngân 1",
                            Email = "cashier@minimart.vn",
                            Role = "CASHIER",
                            StoreId = defaultStore.Id,
                            Status = "ACTIVE",
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        },
                        new User
                        {
                            Username = "warehouse",
                            Password = BCrypt.Net.BCrypt.HashPassword("123456"),
                            FullName = "Nhân Viên Kho",
                            Email = "warehouse@minimart.vn",
                            Role = "WAREHOUSE_STAFF",
                            StoreId = defaultStore.Id,
                            Status = "ACTIVE",
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        }
                    );
                    context.SaveChanges();
                }

                // 3. Nhà cung cấp mặc định
                if (!context.Suppliers.Any())
                {
                    context.Suppliers.Add(new Supplier
                    {
                        SupplierCode = "NCC001",
                        Name = "Công Ty TNHH Thực Phẩm Tiêu Dùng Á Châu",
                        Address = "KCN Tân Bình, TP.HCM",
                        Phone = "0901234567",
                        Email = "supplier@achau.vn",
                        TaxCode = "0312345678",
                        ContactPerson = "Nguyễn Văn Hưng",
                        Status = "ACTIVE",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    });
                    context.SaveChanges();
                }

                var defaultSupplier = context.Suppliers.First();

                // 4. Danh mục sản phẩm
                if (!context.Categories.Any())
                {
                    context.Categories.AddRange(
                        new Category
                        {
                            Name = "Bánh kẹo & Đồ ăn vặt",
                            Description = "Các loại bánh kẹo, snack, socola",
                            CategoryLevel = 1,
                            Status = "ACTIVE",
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        },
                        new Category
                        {
                            Name = "Nước giải khát",
                            Description = "Nước ngọt, nước khoáng, trà xanh, cafe",
                            CategoryLevel = 1,
                            Status = "ACTIVE",
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        },
                        new Category
                        {
                            Name = "Gia vị thực phẩm",
                            Description = "Dầu ăn, nước mắm, mì gói",
                            CategoryLevel = 1,
                            Status = "ACTIVE",
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        }
                    );
                    context.SaveChanges();
                }

                var catBanhKeo = context.Categories.First(c => c.Name.Contains("Bánh"));
                var catNuoc = context.Categories.First(c => c.Name.Contains("Nước"));
                var catGiaVi = context.Categories.First(c => c.Name.Contains("Gia vị"));

                // 5. Sản phẩm mẫu & Tồn kho & Lô hàng
                if (!context.Products.Any())
                {
                    CreateProductWithStock(context, "SP001", "Bánh Chocopie Hộp 12 Cái", "8934567890123", "Hộp", 55000m, catBanhKeo.Id, defaultSupplier.Id, defaultStore.Id, 100);
                    CreateProductWithStock(context, "SP002", "Nước Ngọt Coca Cola 330ml", "8934567890124", "Lon", 10000m, catNuoc.Id, defaultSupplier.Id, defaultStore.Id, 250);
                    CreateProductWithStock(context, "SP003", "Mì Hảo Hảo Tôm Chua Cay", "8934567890125", "Gói", 4500m, catGiaVi.Id, defaultSupplier.Id, defaultStore.Id, 500);
                    CreateProductWithStock(context, "SP004", "Sữa Tươi Tiệt Trùng Vinamilk 1L", "8934567890126", "Hộp", 36000m, catNuoc.Id, defaultSupplier.Id, defaultStore.Id, 80);
                    CreateProductWithStock(context, "SP005", "Dầu Ăn Neptune Light 1L", "8934567890127", "Chai", 48000m, catGiaVi.Id, defaultSupplier.Id, defaultStore.Id, 60);
                    CreateProductWithStock(context, "SP006", "Trà Xanh Không Độ 500ml", "8934567890128", "Chai", 9000m, catNuoc.Id, defaultSupplier.Id, defaultStore.Id, 150);
                }

                // 6. Khách hàng thân thiết mẫu
                if (!context.Customers.Any())
                {
                    context.Customers.Add(new Customer
                    {
                        MemberCode = "KH0001",
                        FullName = "Nguyễn Văn A",
                        Phone = "0987654321",
                        Email = "nguyenvana@gmail.com",
                        Address = "TP. Hồ Chí Minh",
                        TotalPoints = 100,
                        AccumulatedPoints = 100,
                        MembershipTier = "MEMBER",
                        Status = "ACTIVE",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    });
                    context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                // Ghi log lỗi nếu database MySQL chưa bật hoặc cấu hình kết nối cần kiểm tra
                Console.WriteLine($"[DbInitializer Error] {ex.Message}");
            }
        }

        private static void CreateProductWithStock(MiniMartDbContext context, string code, string name, string barcode, string unit, decimal price, long categoryId, long supplierId, long storeId, int stock)
        {
            var product = new Product
            {
                ProductCode = code,
                Name = name,
                Barcode = barcode,
                Unit = unit,
                DefaultPrice = price,
                CategoryId = categoryId,
                SupplierId = supplierId,
                Status = "ACTIVE",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            context.Products.Add(product);
            context.SaveChanges();

            var storeProduct = new StoreProduct
            {
                StoreId = storeId,
                ProductId = product.Id,
                Price = price,
                StockQuantity = stock,
                MinStock = 10,
                MaxStock = 1000,
                Status = "ACTIVE",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            context.StoreProducts.Add(storeProduct);
            context.SaveChanges();

            // Tạo lô hàng mặc định cho sản phẩm
            var batch = new ProductBatch
            {
                StoreProductId = storeProduct.Id,
                ImportDate = DateTime.Today,
                ExpiryDate = DateTime.Today.AddMonths(12),
                Quantity = stock,
                RemainingQuantity = stock,
                ImportPrice = price * 0.75m, // Giá nhập giả định 75% giá bán
                BatchBarcode = $"BATCH-{code}-{DateTime.Today:yyyyMMdd}"
            };
            context.ProductBatches.Add(batch);
            context.SaveChanges();
        }
    }
}


namespace MiniMart.Web.Models.Enums
{
    public enum Role
    {
        ADMIN,
        STORE_MANAGER,
        CASHIER,
        WAREHOUSE_STAFF
    }

    public enum UserStatus
    {
        ACTIVE,
        DISABLED,
        LOCKED
    }

    public enum StoreType
    {
        MINI_MART,
        AGENCY,
        DISTRIBUTOR
    }

    public enum MembershipTier
    {
        MEMBER,
        SILVER,
        GOLD,
        DIAMOND
    }

    public enum ProductStatus
    {
        ACTIVE,
        DISCONTINUED
    }

    public enum InvoiceStatus
    {
        COMPLETED,
        CANCELLED,
        RETURNED
    }

    public enum PaymentMethod
    {
        CASH,
        BANK_TRANSFER,
        E_WALLET
    }

    public enum ImportReceiptStatus
    {
        DRAFT,
        CONFIRMED,
        CANCELLED
    }

    public enum PromotionStatus
    {
        UPCOMING,
        ACTIVE,
        ENDED,
        CANCELLED
    }

    public enum PromotionType
    {
        PERCENTAGE,
        FIXED_AMOUNT,
        BUY_X_GET_Y,
        COMBO
    }

    public enum InventoryTransactionType
    {
        IMPORT,
        SALE,
        TRANSFER_OUT,
        TRANSFER_IN,
        RETURN,
        ADJUSTMENT
    }
}


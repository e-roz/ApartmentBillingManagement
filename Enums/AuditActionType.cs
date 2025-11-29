namespace Apartment.Enums
{
    public enum AuditActionType
    {
        // User Actions
        UserLoginSuccess,
        UserLoginFailure,
        UserLogout,
        CreateUser,
        UpdateUser,
        DeleteUser,
        UpdateUserRole,

        // Billing Actions
        GenerateBills,
        RecordPayment,
        UpdateBill,

        // Request Actions
        SubmitRequest,
        UpdateRequestStatus,
        CloseRequest, // For when a manager replies and deletes

        // Management Actions
        CreateApartment,
        UpdateApartment,
        DeleteApartment,
        AssignTenant,
        UpdateTenant,
        CreateTenant,
        DeleteTenant,

        // System Actions
        SystemAction,
        SystemError
    }
}

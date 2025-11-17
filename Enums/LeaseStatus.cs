namespace Apartment.Enums
{
    public enum LeaseStatus
    {
        // Tenant is interested but hasn't signed a lease (no billing/occupancy)
        Prospective = 0,

        // Tenant is currently under an active lease agreement (billing required)
        Active = 1,

        // Lease has ended or tenant has moved out (no more regular billing)
        Inactive = 2,

        // Tenant was formally evicted (specific status for legal records)
        Evicted = 3
    }
}


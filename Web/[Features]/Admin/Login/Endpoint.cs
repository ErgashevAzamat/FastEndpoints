﻿namespace Admin.Login;

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Verbs(Http.POST, Http.PUT, Http.PATCH);
        Routes("admin/login");
        AllowAnonymous();
        Options(b => b.RequireCors(b => b.AllowAnyOrigin()));
        Describe(b => b
            .Accepts<Request>("application/json")
            .Produces<Response>(200)
            .Produces(400)
            .Produces(403));
        Summary(s =>
        {
            s.Summary = "this is a short summary";
            s.Description = "this is the long description of the endpoint";
            s[200] = "all good";
            s[400] = "indicates an error";
            s[403] = "forbidden when login fails";
            s[201] = "new resource created";
        });
    }

    public override Task HandleAsync(Request r, CancellationToken ct)
    {
        if (r.UserName == "admin" && r.Password == "pass")
        {
            var expiryDate = DateTime.UtcNow.AddDays(1);

            var userPermissions = new[] {
                    Allow.Inventory_Create_Item,
                    Allow.Inventory_Retrieve_Item,
                    Allow.Inventory_Update_Item,
                    Allow.Inventory_Delete_Item,
                    Allow.Customers_Retrieve,
                    Allow.Customers_Create,
                    Allow.Customers_Update};

            var userClaims = new[] {
                    (Claim.UserName, r.UserName),
                    (Claim.UserType, Role.Admin),
                    (Claim.AdminID, "USR0001"),
                    ("test-claim","test claim val")};

            var userRoles = new[] {
                    Role.Admin,
                    Role.Staff };

            var token = JWTBearer.CreateToken(
                Config["TokenKey"],
                expiryDate,
                userPermissions,
                userRoles,
                userClaims);

            return SendAsync(new Response()
            {
                JWTToken = token,
                ExpiryDate = expiryDate,
                Permissions = new Allow().NamesFor(userPermissions)
            });
        }
        else
        {
            AddError("Authentication Failed!");
        }

        return SendErrorsAsync();
    }
}

public class Endpoint_V1 : Endpoint
{
    public override void Configure()
    {
        base.Configure();
        Throttle(5, 5);
        Version(1, deprecateAt: 2);
    }
}

public class Endpoint_V2 : Endpoint<EmptyRequest, object>
{
    public override void Configure()
    {
        Get("admin/login");
        Version(2);
        AllowAnonymous();
    }

    public override Task HandleAsync(EmptyRequest r, CancellationToken ct)
    {
        return SendAsync(2);
    }
}
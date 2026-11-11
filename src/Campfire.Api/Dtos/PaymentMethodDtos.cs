using System.ComponentModel.DataAnnotations;

namespace Campfire.Api.Dtos;

public record PaymentMethodResponse(
    string Id,
    string Brand,
    string Last4,
    long ExpMonth,
    long ExpYear
);

public record SetDefaultPaymentMethodRequest(
    [Required] string PaymentMethodId
);

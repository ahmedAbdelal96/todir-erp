using Modules.Payments.Entities;
using Xunit;

namespace Modules.Payments.Tests;

public sealed class PaymentRulesTests
{
    [Fact]
    public void CustomerReceiptRequiresPartyAndKeepsMovementIndependentFromInvoice()
    {
        var payment = Valid(PaymentDirection.Incoming, PaymentType.CustomerReceipt);

        PaymentRules.Validate(payment);

        Assert.Null(payment.SourceReference);
        Assert.Equal(PaymentDirection.Incoming, payment.Direction);
    }

    [Theory]
    [InlineData(PaymentDirection.Incoming, PaymentType.SupplierPayment)]
    [InlineData(PaymentDirection.Outgoing, PaymentType.CustomerAdvance)]
    public void PartyPaymentTypeMustMatchMoneyDirection(PaymentDirection direction, PaymentType type)
    {
        var error = Assert.Throws<PaymentException>(() => PaymentRules.Validate(Valid(direction, type)));

        Assert.Equal("payments.payment_type_direction_invalid", error.Code);
    }

    [Fact]
    public void PartyBearingPaymentsRequireParty()
    {
        var error = Assert.Throws<PaymentException>(() => PaymentRules.Validate(Valid(PaymentDirection.Incoming, PaymentType.CustomerAdvance) with { PartyId = null }));

        Assert.Equal("payments.party_required", error.Code);
    }

    [Fact]
    public void BankOriginatedUnknownReceiptMayRemainUnallocatedWithoutInventingParty()
    {
        var payment = Valid(PaymentDirection.Incoming, PaymentType.CustomerReceipt) with
        {
            PartyId = null,
            SourceType = "BankingUnknownReceipt",
            SourceReference = "bank-statement:line-1"
        };

        PaymentRules.Validate(payment);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("0.0000001")]
    [InlineData("NaN")]
    [InlineData("100000000000000")]
    public void FinancialAmountsMustBePositiveExactDecimals(string amount)
    {
        var error = Assert.Throws<PaymentException>(() => PaymentRules.Validate(Valid(PaymentDirection.Incoming, PaymentType.CustomerReceipt) with { Amount = amount }));

        Assert.Equal("payments.amount_invalid", error.Code);
    }

    [Fact]
    public void IdempotencyAndExternalReferencesHaveBoundedLengths()
    {
        var idempotencyError = Assert.Throws<PaymentException>(() => PaymentRules.Validate(Valid(PaymentDirection.Incoming, PaymentType.CustomerReceipt) with
        {
            IdempotencyKey = new string('k', 201)
        }));
        var referenceError = Assert.Throws<PaymentException>(() => PaymentRules.Validate(Valid(PaymentDirection.Incoming, PaymentType.CustomerReceipt) with
        {
            BankReference = new string('r', 241)
        }));

        Assert.Equal("payments.request_invalid", idempotencyError.Code);
        Assert.Equal("payments.reference_invalid", referenceError.Code);
    }

    [Fact]
    public void InternalTransfersCannotBeCreatedAsPartyPayments()
    {
        var error = Assert.Throws<PaymentException>(() => PaymentRules.Validate(Valid(PaymentDirection.InternalTransfer, PaymentType.TransferLeg)));

        Assert.Equal("payments.transfer_invalid", error.Code);
    }

    [Theory]
    [InlineData(PaymentDirection.Incoming, PaymentType.CustomerReceipt)]
    [InlineData(PaymentDirection.Incoming, PaymentType.CustomerAdvance)]
    [InlineData(PaymentDirection.Incoming, PaymentType.SupplierRefund)]
    [InlineData(PaymentDirection.Outgoing, PaymentType.SupplierPayment)]
    [InlineData(PaymentDirection.Outgoing, PaymentType.SupplierAdvance)]
    [InlineData(PaymentDirection.Outgoing, PaymentType.CustomerRefund)]
    [InlineData(PaymentDirection.Outgoing, PaymentType.ExpensePayment)]
    [InlineData(PaymentDirection.Outgoing, PaymentType.Standalone)]
    public void ApprovedManualPaymentTypesMustMatchTheirMovementDirection(PaymentDirection direction, PaymentType type)
    {
        PaymentRules.Validate(Valid(direction, type));
    }

    [Theory]
    [InlineData(PaymentDirection.Incoming, PaymentType.SupplierPayment)]
    [InlineData(PaymentDirection.Incoming, PaymentType.CustomerRefund)]
    [InlineData(PaymentDirection.Outgoing, PaymentType.CustomerReceipt)]
    [InlineData(PaymentDirection.Outgoing, PaymentType.SupplierRefund)]
    public void CrossedPaymentTypeAndDirectionIsRejected(PaymentDirection direction, PaymentType type)
    {
        var error = Assert.Throws<PaymentException>(() => PaymentRules.Validate(Valid(direction, type)));
        Assert.Equal("payments.payment_type_direction_invalid", error.Code);
    }

    private static CreatePaymentCommand Valid(PaymentDirection direction, PaymentType type) => new(
        Guid.CreateVersion7(), null, direction, type, Guid.CreateVersion7(), "1250.75", "EGP", PaymentMethod.BankTransfer,
        Guid.CreateVersion7(), new(2026, 10, 1), new(2026, 10, 1), null, "TRX-44", null, null, "Manual", null, "idempotency-1");
}

using System;
using Inferno.Core.Ownership;
using Xunit;

namespace Inferno.Core.Tests.Ownership;

public class FireplaceRpcTests
{
    [Theory]
    [InlineData(0f, 10f, 1f)]
    [InlineData(3.2f, 10f, 4.2f)]
    [InlineData(9.2f, 10f, 9.2f)]   // shows as 10/10 already (rounded up): vanilla refuses
    [InlineData(10f, 10f, 10f)]
    [InlineData(-5f, 10f, 1f)]      // corrupt → treated as empty
    [InlineData(float.NaN, 10f, 1f)]
    public void AddOne_MatchesVanilla(float fuel, float max, float expected) =>
        Assert.Equal(expected, FireplaceRpc.AddOne(fuel, max), precision: 4);

    [Theory]
    [InlineData(5f, 2f, 10f, 7f)]
    [InlineData(5f, 20f, 10f, 10f)]
    [InlineData(5f, -2f, 10f, 3f)]
    [InlineData(5f, -20f, 10f, 0f)]
    [InlineData(5f, float.NaN, 10f, 5f)]
    public void AddAmount_ClampsToTank(float fuel, float amount, float max, float expected) =>
        Assert.Equal(expected, FireplaceRpc.AddAmount(fuel, amount, max), precision: 4);

    [Theory]
    [InlineData(4f, 10f, 4f)]
    [InlineData(40f, 10f, 10f)]
    [InlineData(-1f, 10f, 0f)]
    [InlineData(float.PositiveInfinity, 10f, 0f)]
    public void SetAmount_ClampsToTank(float fuel, float max, float expected) =>
        Assert.Equal(expected, FireplaceRpc.SetAmount(fuel, max), precision: 4);

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(0, 1)]  // anything but "on" becomes on, like vanilla
    public void Toggle_MatchesVanilla(int state, int expected) =>
        Assert.Equal(expected, FireplaceRpc.Toggle(state));
}

public class OwnershipPolicyTests
{
    [Theory]
    // mode, eligible, ownedByServer, onLoan → action
    [InlineData(true, true, false, false, OwnershipAction.Claim)]
    [InlineData(true, true, true, false, OwnershipAction.None)]
    [InlineData(true, true, false, true, OwnershipAction.None)]    // lent to a player: wait
    [InlineData(true, true, true, true, OwnershipAction.None)]
    [InlineData(false, true, true, false, OwnershipAction.Release)] // mode switched off: give back
    [InlineData(false, true, false, false, OwnershipAction.None)]
    [InlineData(true, false, true, false, OwnershipAction.Release)] // not eligible (e.g. snow-melting fire)
    [InlineData(true, false, false, false, OwnershipAction.None)]
    public void Decide(bool mode, bool eligible, bool owned, bool onLoan, OwnershipAction expected) =>
        Assert.Equal(expected, OwnershipPolicy.Decide(mode, eligible, owned, onLoan));
}

public class LoanBookTests
{
    [Fact]
    public void Loan_ExpiresAfterDuration()
    {
        var book = new LoanBook<int>();
        book.Lend(1, now: 10, seconds: 30);

        Assert.True(book.IsOnLoan(1, 10));
        Assert.True(book.IsOnLoan(1, 39.9));
        Assert.False(book.IsOnLoan(1, 40));
        Assert.False(book.IsOnLoan(2, 10));
    }

    [Fact]
    public void Lend_ExtendsButNeverShortens()
    {
        var book = new LoanBook<int>();
        book.Lend(1, now: 0, seconds: 30);
        book.Lend(1, now: 20, seconds: 30);   // extends to 50
        book.Lend(1, now: 21, seconds: 1);    // would shorten: ignored

        Assert.True(book.IsOnLoan(1, 49));
        Assert.False(book.IsOnLoan(1, 50));
    }

    [Fact]
    public void Prune_RemovesOnlyExpired()
    {
        var book = new LoanBook<int>();
        book.Lend(1, now: 0, seconds: 10);
        book.Lend(2, now: 0, seconds: 30);

        book.Prune(5);
        Assert.Equal(2, book.Count);

        book.Prune(10);
        Assert.Equal(1, book.Count);
        Assert.True(book.IsOnLoan(2, 10));

        book.Clear();
        Assert.Equal(0, book.Count);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void Lend_InvalidDuration_Throws(double seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>("seconds", () => new LoanBook<int>().Lend(1, 0, seconds));
}

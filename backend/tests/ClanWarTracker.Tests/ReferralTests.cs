using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Infrastructure.Persistence.Repositories;

namespace ClanWarTracker.Tests;

/// <summary>Награда за друга - +3 дня Плюса, но не больше 30 дней за 30 дней: зовут друзей, а не твинков.</summary>
public class ReferralTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ReferralDays_SumOnlyRecentNotRevokedOfThatSource()
    {
        var now = DateTime.UtcNow;
        await using (var db = _db.New())
        {
            Entitlement E(string source, int days, DateTime created, DateTime? revoked = null) => new()
            {
                TelegramUserId = 7, Sku = Entitlement.Skus.Plus, Source = source, Days = days,
                UntilUtc = created.AddDays(days), CreatedAtUtc = created, RevokedAtUtc = revoked,
            };
            db.Entitlements.AddRange(
                E(Entitlement.Sources.Referral, 3, now.AddDays(-1)),
                E(Entitlement.Sources.Referral, 3, now.AddDays(-5)),
                E(Entitlement.Sources.Referral, 3, now.AddDays(-40)),          // старше 30 дней
                E(Entitlement.Sources.Referral, 3, now.AddDays(-2), now),      // отозвана
                E(Entitlement.Sources.Gift, 30, now.AddDays(-1)));             // другой источник
            await db.SaveChangesAsync();
        }

        await using (var db = _db.New())
        {
            var days = await new EntitlementRepository(db).SumDaysSinceAsync(7, Entitlement.Sources.Referral, now.AddDays(-30));
            Assert.Equal(6, days);
        }
    }

    [Fact]
    public void Cap_AllowsTenFriendsPerMonth()
    {
        Assert.Equal(10, LinkPlayerUseCase.ReferralDaysPer30 / LinkPlayerUseCase.ReferralDays);
    }
}

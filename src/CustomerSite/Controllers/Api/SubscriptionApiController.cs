// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Linq;
using Marketplace.SaaS.Accelerator.DataAccess.Contracts;
using Marketplace.SaaS.Accelerator.Services.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.SaaS.Accelerator.CustomerSite.Controllers.Api;

/// <summary>
/// Subscription API Controller for external integrations.
/// </summary>
[Route("api/subscription")]
[ApiController]
public class SubscriptionApiController : ControllerBase
{
    private readonly ISubscriptionsRepository subscriptionRepository;
    private readonly IPlansRepository planRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionApiController"/> class.
    /// </summary>
    /// <param name="subscriptionRepository">The subscription repository.</param>
    /// <param name="planRepository">The plan repository.</param>
    public SubscriptionApiController(
        ISubscriptionsRepository subscriptionRepository,
        IPlansRepository planRepository)
    {
        this.subscriptionRepository = subscriptionRepository;
        this.planRepository = planRepository;
    }

    /// <summary>
    /// Checks if a user has an active subscription.
    /// </summary>
    /// <param name="email">The user's email address.</param>
    /// <returns>Subscription status response.</returns>
    [HttpGet("check")]
    public IActionResult CheckUserSubscription([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { error = "Email is required" });
        }

        try
        {
            var subscriptionService = new SubscriptionService(this.subscriptionRepository, this.planRepository);
            var subscriptions = subscriptionService.GetPartnerSubscription(email, default, true).ToList();

            if (!subscriptions.Any())
            {
                return Ok(new
                {
                    hasSubscription = false,
                    email = email,
                    message = "No subscriptions found for this user"
                });
            }

            var activeSubscriptionCount = subscriptions.Count(s =>
                s.IsActiveSubscription &&
                s.SubscriptionStatus == Services.Models.SubscriptionStatusEnumExtension.Subscribed);

            var response = new
            {
                hasSubscription = activeSubscriptionCount > 0,
                email = email,
                totalSubscriptions = subscriptions.Count,
                activeSubscriptionCount = activeSubscriptionCount,
                subscriptions = subscriptions.Select(s => new
                {
                    subscriptionId = s.Id,
                    name = s.Name,
                    status = s.SubscriptionStatus.ToString(),
                    isActive = s.IsActiveSubscription,
                    planId = s.PlanId,
                    offerId = s.OfferId,
                    quantity = s.Quantity
                }).ToList()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "An error occurred while checking subscription", details = ex.Message });
        }
    }
}

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
    private readonly IApplicationConfigRepository applicationConfigRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionApiController"/> class.
    /// </summary>
    /// <param name="subscriptionRepository">The subscription repository.</param>
    /// <param name="planRepository">The plan repository.</param>
    /// <param name="applicationConfigRepository">The application config repository.</param>
    public SubscriptionApiController(
        ISubscriptionsRepository subscriptionRepository,
        IPlansRepository planRepository,
        IApplicationConfigRepository applicationConfigRepository)
    {
        this.subscriptionRepository = subscriptionRepository;
        this.planRepository = planRepository;
        this.applicationConfigRepository = applicationConfigRepository;
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

            var activeSubscriptions = subscriptions.Where(s => s.IsActiveSubscription).ToList();
            var subscribedSubscriptions = subscriptions.Where(s => 
                s.SubscriptionStatus == Services.Models.SubscriptionStatusEnumExtension.Subscribed).ToList();

            var response = new
            {
                hasSubscription = activeSubscriptions.Any() || subscribedSubscriptions.Any(),
                email = email,
                totalSubscriptions = subscriptions.Count,
                activeSubscriptions = activeSubscriptions.Count,
                subscribedSubscriptions = subscribedSubscriptions.Count,
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

    /// <summary>
    /// Checks subscription status by subscription ID.
    /// </summary>
    /// <param name="subscriptionId">The subscription identifier.</param>
    /// <returns>Subscription details.</returns>
    [HttpGet("{subscriptionId}")]
    public IActionResult GetSubscriptionById(Guid subscriptionId)
    {
        if (subscriptionId == Guid.Empty)
        {
            return BadRequest(new { error = "Valid subscription ID is required" });
        }

        try
        {
            var subscriptionService = new SubscriptionService(this.subscriptionRepository, this.planRepository);
            var subscription = subscriptionService.GetSubscriptionsBySubscriptionId(subscriptionId, true);

            if (subscription == null || subscription.SubscribeId == 0)
            {
                return NotFound(new { error = "Subscription not found" });
            }

            var planDetail = this.planRepository.GetById(subscription.PlanId);

            var response = new
            {
                subscriptionId = subscription.Id,
                name = subscription.Name,
                status = subscription.SubscriptionStatus.ToString(),
                isActive = subscription.IsActiveSubscription,
                planId = subscription.PlanId,
                offerId = subscription.OfferId,
                quantity = subscription.Quantity,
                customerEmail = subscription.CustomerEmailAddress,
                customerName = subscription.CustomerName,
                isMeteringSupported = subscription.IsMeteringSupported,
                isPerUserPlan = planDetail?.IsPerUser ?? false,
                term = new
                {
                    startDate = subscription.Term?.StartDate,
                    endDate = subscription.Term?.EndDate,
                    termUnit = subscription.Term?.TermUnit.ToString()
                },
                purchaser = new
                {
                    email = subscription.Purchaser?.EmailId,
                    tenantId = subscription.Purchaser?.TenantId
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "An error occurred while retrieving subscription", details = ex.Message });
        }
    }

    /// <summary>
    /// Validates if a user has any active subscribed status.
    /// </summary>
    /// <param name="email">The user's email address.</param>
    /// <returns>Simple validation response.</returns>
    [HttpGet("validate")]
    public IActionResult ValidateUserSubscription([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { error = "Email is required" });
        }

        try
        {
            var subscriptionService = new SubscriptionService(this.subscriptionRepository, this.planRepository);
            var subscriptions = subscriptionService.GetPartnerSubscription(email, default, false).ToList();

            var hasActiveSubscription = subscriptions.Any(s => 
                s.IsActiveSubscription && 
                s.SubscriptionStatus == Services.Models.SubscriptionStatusEnumExtension.Subscribed);

            return Ok(new
            {
                isValid = hasActiveSubscription,
                email = email,
                message = hasActiveSubscription ? "User has active subscription" : "User does not have active subscription"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "An error occurred while validating subscription", details = ex.Message });
        }
    }
}

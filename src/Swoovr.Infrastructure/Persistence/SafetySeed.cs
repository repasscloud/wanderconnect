using Swoovr.Domain;

namespace Swoovr.Infrastructure.Persistence;

/// <summary>
/// The first Safety &amp; Support packs (#103, its 04-RESOURCE-CATALOGUE; D204). AU is the list the owner approved for the
/// Adelaide beta in Q77 (D190, 27 October 2026), so it starts Ready with that date; NZ is populated but not verified
/// (not launch-ready); GB is reference data only (Demo). Admins edit all of it in the admin app; every change is a revision.
/// </summary>
public static class SafetySeed
{
    private static readonly DateTimeOffset Seeded = new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Approved = new(2026, 10, 27), Due = new(2027, 1, 25);

    public static readonly SafetyCountry[] Countries =
    [
        new() { Code = "AU", Name = "Australia", Enabled = true, Readiness = SafetyReadiness.Ready, LastReviewedOn = Approved, ReviewDueOn = Due,
            Owner = "Trust & Safety", Notes = "Owner-approved list for the Adelaide beta (Q77, D190).", Version = 1, UpdatedAt = Seeded },
        new() { Code = "NZ", Name = "New Zealand", Enabled = true, Readiness = SafetyReadiness.NotReady, Owner = "Trust & Safety",
            Notes = "Starter entries; research and verify every category before enabling an NZ market.", Version = 1, UpdatedAt = Seeded },
        new() { Code = "GB", Name = "United Kingdom", Enabled = true, Readiness = SafetyReadiness.Demo, Owner = "Trust & Safety",
            Notes = "Illustrative only; not approved for launch.", Version = 1, UpdatedAt = Seeded },
    ];

    private static SafetyResource R(string id, string country, SafetyResourceCategory category, int order, string name, string description,
        string? phone, string? display, string? web, string source, bool verified) => new()
    {
        Id = Guid.Parse(id), CountryCode = country, LanguageCode = country == "AU" ? "en-AU" : country == "NZ" ? "en-NZ" : "en-GB", Category = category,
        DisplayOrder = order, Name = name, Description = description, Phone = phone, PhoneDisplay = display, WebsiteUrl = web, SourceUrl = source,
        Enabled = true, LastVerifiedOn = verified ? Approved : null, ReviewDueOn = verified ? Due : null, UpdatedAt = Seeded,
    };

    public static readonly SafetyResource[] Resources =
    [
        // Australia Safety Resources
        R("6f1a0c1e-0001-4000-8000-000000000001", "AU", SafetyResourceCategory.Emergency, 10, "Triple Zero (000)", "Police, fire and ambulance. Call if you or someone else is in danger now.",
            "000", "000", "https://www.triplezero.gov.au/", "https://www.triplezero.gov.au/", true),
        R("6f1a0c1e-0001-4000-8000-000000000002", "AU", SafetyResourceCategory.PoliceNonEmergency, 20, "Police Assistance Line", "Report a crime to police when it isn't an emergency.",
            "131444", "131 444", "https://www.police.sa.gov.au/", "https://www.police.sa.gov.au/", true),
        R("6f1a0c1e-0001-4000-8000-000000000003", "AU", SafetyResourceCategory.SexualViolence, 30, "1800RESPECT", "24/7 support for sexual assault and domestic or family violence.",
            "1800737732", "1800 737 732", "https://www.1800respect.org.au/", "https://www.1800respect.org.au/", true),
        R("6f1a0c1e-0001-4000-8000-000000000004", "AU", SafetyResourceCategory.Crisis, 40, "Lifeline", "24/7 crisis support.",
            "131114", "13 11 14", "https://www.lifeline.org.au/", "https://www.lifeline.org.au/", true),
        R("6f1a0c1e-0001-4000-8000-000000000005", "AU", SafetyResourceCategory.OnlineSafety, 50, "eSafety Commissioner", "Report serious online abuse and image-based abuse.",
            null, null, "https://www.esafety.gov.au/report", "https://www.esafety.gov.au/report", true),
        R("6f1a0c1e-0001-4000-8000-000000000006", "AU", SafetyResourceCategory.ChildExploitation, 60, "ACCCE", "Report online child sexual exploitation to the Australian Centre to Counter Child Exploitation.",
            null, null, "https://www.accce.gov.au/report", "https://www.accce.gov.au/report", true),
        
        // New Zealand Safety Resources
        R("6f1a0c1e-0002-4000-8000-000000000001", "NZ", SafetyResourceCategory.Emergency, 10, "Emergency services (111)", "Police, fire or ambulance in an emergency.",
            "111", "111", "https://www.police.govt.nz/", "https://www.police.govt.nz/advice-services/how-report-crime-or-incident", true),
        R("6f1a0c1e-0002-4000-8000-000000000002", "NZ", SafetyResourceCategory.PoliceNonEmergency, 20, "New Zealand Police 105", "Non-urgent police matters.",
            "105", "105", "https://www.police.govt.nz/105support", "https://www.police.govt.nz/faq/why-has-police-introduced-non-emergency-number", false),
        R("6f1a0c1e-0002-4000-8000-000000000002", "NZ", SafetyResourceCategory.PoliceNonEmergency, 20,
            "New Zealand Police 105",
            "Report crimes or incidents that do not require an immediate police response. Available 24/7.",
            "105", "105",
            "https://105.police.govt.nz/",
            "https://www.police.govt.nz/105support", true),

        R("6f1a0c1e-0002-4000-8000-000000000004", "NZ", SafetyResourceCategory.Crisis, 30,
            "Need to Talk? 1737",
            "Free, confidential emotional and mental health support, available 24/7 by phone or text.",
            "1737", "1737",
            "https://1737.org.nz/",
            "https://1737.org.nz/how-1737-works", true),

        R("6f1a0c1e-0002-4000-8000-000000000005", "NZ", SafetyResourceCategory.SexualViolence, 40,
            "Safe to Talk",
            "Free, confidential 24/7 support for people affected by sexual harm, assault or abuse. Call or text 4334.",
            "0800044334", "0800 044 334",
            "https://www.safetotalk.nz/",
            "https://www.safetotalk.nz/contact-us/", true),

        R("6f1a0c1e-0002-4000-8000-000000000006", "NZ", SafetyResourceCategory.DomesticViolence, 45,
            "Are You OK",
            "Free, confidential 24/7 support for anyone affected by domestic or family violence, including safety planning and referrals.",
            "0800456450", "0800 456 450",
            "https://www.areyouok.org.nz/",
            "https://www.areyouok.org.nz/get-support/", true),

        R("6f1a0c1e-0002-4000-8000-000000000009", "NZ", SafetyResourceCategory.DomesticViolence, 46,
            "Women's Refuge",
            "Free 24/7 crisis support and assistance for women and children experiencing domestic or family violence.",
            "0800733843", "0800 733 843",
            "https://womensrefuge.org.nz/",
            "https://womensrefuge.org.nz/", true),

        R("6f1a0c1e-0002-4000-8000-000000000003", "NZ", SafetyResourceCategory.OnlineSafety, 50,
            "Netsafe",
            "Report online abuse, harassment, bullying or image-based abuse. Phone support Monday to Friday, 9am to 5pm.",
            "0508638723", "0508 638 723",
            "https://netsafe.org.nz/report",
            "https://netsafe.org.nz/netsafe/contact-us", true),

        R("6f1a0c1e-0002-4000-8000-000000000007", "NZ", SafetyResourceCategory.ChildExploitation, 60,
            "Department of Internal Affairs - Digital Child Exploitation Team",
            "Report suspected online child sexual abuse or exploitation material. For grooming or immediate danger, contact Police.",
            null, null,
            "https://www.dia.govt.nz/Digital-Safety-Report-Online-Child-Exploitation-Material",
            "https://www.dia.govt.nz/Digital-Safety-Report-Online-Child-Exploitation-Material", true),

        R("6f1a0c1e-0002-4000-8000-000000000008", "NZ", SafetyResourceCategory.Other, 70,
            "Victim Support",
            "Free, confidential 24/7 support for victims of crime, their families and witnesses of traumatic events.",
            "0800842846", "0800 842 846",
            "https://www.victimsupport.org.nz/",
            "https://www.victimsupport.org.nz/", true),

        // United Kingdom Safety Resources
        // R("6f1a0c1e-0003-4000-8000-000000000001", "GB", SafetyResourceCategory.Emergency, 10, "Emergency services (999)", "Police, fire or ambulance in an emergency.",
        //     "999", "999", "https://www.gov.uk/contact-police", "https://www.gov.uk/contact-police", false),
        // R("6f1a0c1e-0003-4000-8000-000000000002", "GB", SafetyResourceCategory.PoliceNonEmergency, 20, "Police 101", "Non-emergency police contact.",
        //     "101", "101", "https://www.police.uk/", "https://www.police.uk/", false),
        // R("6f1a0c1e-0003-4000-8000-000000000003", "GB", SafetyResourceCategory.ChildExploitation, 60, "CEOP Safety Centre", "Report online child sexual abuse concerns.",
        //     null, null, "https://www.ceop.police.uk/Safety-Centre/", "https://www.ceop.police.uk/Safety-Centre/", false),
    ];
}

# Issue intake

Use this file to investigate reported items and create GitHub issues.
Do not implement fixes unless separately requested.

## 1. Short, descriptive title

- Status: Ready for investigation
- Area: Member web / Admin / API / Docker / Mobile
- Page or URL:
- Type: Bug / Improvement / Question
- Priority: High / Medium / Low
- GitHub issue: Not created
- Tracking Q: Not assigned

### What happened
Describe the behaviour you observed.

### What I expected
Describe what should happen instead, or say “Needs investigation”.

### Steps to reproduce
1.
2.
3.

### Evidence
Paste relevant error messages or link screenshots.
Do not include passwords, tokens or real personal information.

### Impact
Who is affected, and what does it prevent?

### Notes or constraints
Optional context, suggested explanations or requirements.

## 2. Cannot search for accounts by email address

- Status: Ready for investigation
- Area: Admin
- Page or URL: http://localhost:5082/accounts?email=fictional.alex%40example.test
- Type: Bug
- Priority: Medium
- GitHub issue: Not created
- Tracking Q: Not assigned

### What happened
When I search for a user with an existing email address that I know exists (I am logged in as user, and verified email address) it does not show the user account in the search. It comes back with "No account with that email.". 

### What I expected
Find the account and let me use my admin view to see/review the account

### Steps to reproduce
1. Login as admin
2. Navigate to `/accounts`
3. Search for email address of account

### Evidence
Paste relevant error messages or link screenshots.
Do not include passwords, tokens or real personal information.

### Impact
Admins cannot find an account when doing look up.

### Notes or constraints
Not applicable.

## 3. Navigating around admin UI get session ended text

- Status: Ready for investigation
- Area: Admin
- Page or URL: http://localhost:5082/media
- Type: Bug
- Priority: High
- GitHub issue: Not created
- Tracking Q: Not assigned

### What happened
When navigating around the admin area, I sometimes get a text message "Your session has ended. Sign in again." The account still hasn't been "signed out" and the "Sign out" button is still present. Shouldn't it kick me back to the login screen for admin then?

### What I expected
It to always show the page I am logged in or logged out, not have a weak message requiring me to click "Sign out" button, then click "Sign in" again (on a non-swoovr designed page!!!) and then go abck and voila!

### Steps to reproduce
1. Navigaete to a swoovr admin page
2. See error message
3. Clicks sign out button
4. Click sign in text on non-branded page (that needs to be fixed)
5. Resume normal work

### Evidence
Paste relevant error messages or link screenshots.
Do not include passwords, tokens or real personal information.

### Impact
Additional steps creating friction in the workflow not required. Also has opportunity for people to use "." as reason, should be drop down text options.

### Notes or constraints
Not applicable

## 4. Photos asking for reason to view to verify

- Status: Ready for investigation
- Area: Admin
- Page or URL: http://localhost:5082/media
- Type: Improvement
- Priority: High
- GitHub issue: Not created
- Tracking Q: Not assigned

### What happened
Opening the photo review portaal, I see there is a text box for me to fill in for "Look at this photo" coming up. The field requires me to fill it in.

### What I expected
This is the photo review screen, the only reason I need to view the photo is to review it. Why is that coming up at all? I should be able to press the "Reval photo" button and see the photo blurred like it is (second screenshot) and then press "Reveal" button/text to show it. For the approved drop down, I expect there to be pre-set approvals that come up there. This applies to all photos. What does the "Reject" need to exist for? Why are we rejecting it - that should have an option in the drop downs too. Also, if users have "enabled adult photos in private chat" - then it shouldn't send the photo to queue to be reviewed, it should go straight through.

### Steps to reproduce
1. Navigate to http://localhost:5082/media
2. Add reason to show photo
3. Click "Show photo" button
3. Click reveal text to display
4. Add reason to approve/reject

### Evidence
Paste relevant error messages or link screenshots.
Do not include passwords, tokens or real personal information.

### Impact
Creates unecessary work and doesn't give reasons to choose from to ensure we can review the information at a later stage to see how many photos approved/rejected - and the categories.

### Notes or constraints
Not applicable


## 5. Beta enrollments screen is messey

- Status: Ready for investigation
- Area: Admin
- Page or URL: http://localhost:5082/beta
- Type: Improvement
- Priority: High
- GitHub issue: Not created
- Tracking Q: Not assigned

### What happened
The Beta enrollment screen is a mess. It should be a table format display to make it easy to search and navigate. Currently it's showing like blocks of users. This will become a problem when we have 1000+ beta users on the application before going live.

### What I expected
Table format that is able to be useed. Ensure buttons do not touch eiether, the "Record staff-reported state" and "Confirm revocation" button are vertically touching, and hard to see. 

### Steps to reproduce
1. Not applicable

### Evidence
Paste relevant error messages or link screenshots.
Do not include passwords, tokens or real personal information.

### Impact
Who is affected, and what does it prevent?

### Notes or constraints
Optional context, suggested explanations or requirements.

## 6. Short, descriptive title

- Status: Ready for investigation
- Area: Member web / Admin / API / Docker / Mobile
- Page or URL:
- Type: Bug / Improvement / Question
- Priority: High / Medium / Low
- GitHub issue: Not created
- Tracking Q: Not assigned

### What happened
Describe the behaviour you observed.

### What I expected
Describe what should happen instead, or say “Needs investigation”.

### Steps to reproduce
1.
2.
3.

### Evidence
Paste relevant error messages or link screenshots.
Do not include passwords, tokens or real personal information.

### Impact
Who is affected, and what does it prevent?

### Notes or constraints
Optional context, suggested explanations or requirements.

## 1. Short, descriptive title

- Status: Ready for investigation
- Area: Member web / Admin / API / Docker / Mobile
- Page or URL:
- Type: Bug / Improvement / Question
- Priority: High / Medium / Low
- GitHub issue: Not created
- Tracking Q: Not assigned

### What happened
Describe the behaviour you observed.

### What I expected
Describe what should happen instead, or say “Needs investigation”.

### Steps to reproduce
1.
2.
3.

### Evidence
Paste relevant error messages or link screenshots.
Do not include passwords, tokens or real personal information.

### Impact
Who is affected, and what does it prevent?

### Notes or constraints
Optional context, suggested explanations or requirements.

## 1. Short, descriptive title

- Status: Ready for investigation
- Area: Member web / Admin / API / Docker / Mobile
- Page or URL:
- Type: Bug / Improvement / Question
- Priority: High / Medium / Low
- GitHub issue: Not created
- Tracking Q: Not assigned

### What happened
Describe the behaviour you observed.

### What I expected
Describe what should happen instead, or say “Needs investigation”.

### Steps to reproduce
1.
2.
3.

### Evidence
Paste relevant error messages or link screenshots.
Do not include passwords, tokens or real personal information.

### Impact
Who is affected, and what does it prevent?

### Notes or constraints
Optional context, suggested explanations or requirements.

## 1. Short, descriptive title

- Status: Ready for investigation
- Area: Member web / Admin / API / Docker / Mobile
- Page or URL:
- Type: Bug / Improvement / Question
- Priority: High / Medium / Low
- GitHub issue: Not created
- Tracking Q: Not assigned

### What happened
Describe the behaviour you observed.

### What I expected
Describe what should happen instead, or say “Needs investigation”.

### Steps to reproduce
1.
2.
3.

### Evidence
Paste relevant error messages or link screenshots.
Do not include passwords, tokens or real personal information.

### Impact
Who is affected, and what does it prevent?

### Notes or constraints
Optional context, suggested explanations or requirements.

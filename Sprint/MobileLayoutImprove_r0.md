# Mobile Layout Improvement - Chat Page (R0)

**Repository:** JaredCH/myface  
**Sprint Start Date:** 2026-01-05  
**Status:** Planning Phase  
**Current User:** JaredCH

---

## Overview

This document outlines the creation of a mobile-optimized chat interface for the `/chat` page. The current desktop layout performs poorly on mobile devices.  This sprint will create a parallel mobile-first design while preserving the existing desktop experience.

This is a living document - AI agents should check items as completed and add notes without overwriting existing content.

---

## Problem Statement

- Current `/chat` page has poor mobile layout
- Desktop-centric design doesn't translate well to small screens
- Mobile users need a native-feeling messaging experience similar to SMS apps

---

## Solution Architecture

### Dual-View System

1. **Desktop View:** `/chat` - Existing layout (unchanged for desktop users)
2. **Mobile View:** `/chat-mobile` - New mobile-optimized layout
3. **Auto-Detection:** Automatically route mobile devices to `/chat-mobile`
4. **Manual Override:** Allow users to switch between views regardless of device

### Mobile Design Principles

- **Native Feel:** Mimic iOS/Android messaging apps (iMessage, WhatsApp, SMS)
- **Minimal UI:** Remove unnecessary chrome, maximize message area
- **Touch-Optimized:** Large tap targets, swipe gestures where appropriate
- **Simple & Clean:** Easy to read messages, clear send button
- **Fast Loading:** Optimize for mobile networks
- **Responsive:** Work across all mobile screen sizes (320px - 768px)

### Key Features

- Full-height viewport usage
- Fixed header with conversation info
- Scrollable message area (majority of screen)
- Fixed bottom input area
- Clear visual distinction between sent/received messages
- Timestamps (subtle, not intrusive)
- View toggle link (switch to desktop view)

---

## Phase 1: Discovery & Current State Analysis

### Step 1.1: Audit Current Chat Page ⬜

- [ ] Document current `/chat` route and controller
- [ ] Identify all views related to chat functionality
- [ ] Map database models/entities for chat/messages
- [ ] Document current JavaScript functionality
- [ ] Identify how messages are sent/received (AJAX, SignalR, WebSockets, etc.)
- [ ] Document current CSS for chat page
- [ ] Take screenshots of current mobile experience (for comparison)
- [ ] List all chat features (send, receive, typing indicators, read receipts, etc.)

**Files to investigate:**
- Controllers:  `*Chat*Controller.cs`
- Views: `Views/Chat/` directory
- Models: Chat/Message related entities
- JavaScript: Chat-related JS files
- CSS: Chat styling files
- SignalR hubs (if using real-time updates)

**Document here:**
- **Controller:** _[TO BE FILLED]_
- **Views:** _[TO BE FILLED]_
- **Models:** _[TO BE FILLED]_
- **JavaScript:** _[TO BE FILLED]_
- **CSS:** _[TO BE FILLED]_
- **Real-time tech:** _[TO BE FILLED - SignalR/WebSockets/Polling]_

### Step 1.2: Identify Mobile Detection Strategy ⬜

**Options to evaluate:**

- [ ] **Option A:** Server-side User-Agent detection
  - Pros: Works immediately, no JS required
  - Cons: Can be spoofed, not always accurate
  
- [ ] **Option B:** Client-side detection (JavaScript)
  - Pros: Can check screen size, touch support
  - Cons: Requires JS, slight delay
  
- [ ] **Option C:** CSS Media Queries only
  - Pros: Pure CSS, no detection needed
  - Cons:  Doesn't allow separate routes
  
- [ ] **Option D:** Hybrid (Server-side redirect + client-side enhancement)
  - Pros: Best of both worlds
  - Cons: More complex

**Recommended Approach:** Option D (Hybrid)
- Server detects mobile User-Agent, redirects to `/chat-mobile`
- Client-side JS enhances with screen size detection
- Store user preference in cookie/localStorage
- Respect manual override

**Implementation notes:**
- [ ] Research C# mobile detection libraries (e.g., `Wangkanai. Detection`, `51Degrees`)
- [ ] Document chosen library and version
- [ ] Plan cookie/localStorage schema for user preference

**Chosen approach:** _[TO BE FILLED]_

### Step 1.3: Define Mobile Breakpoints ⬜

- [ ] Define what constitutes "mobile" (screen width)
- [ ] Define tablet handling strategy (use mobile or desktop?)
- [ ] Document breakpoint values

**Recommended breakpoints:**
- Mobile: `max-width: 767px`
- Tablet: `768px - 1024px` - _[DECISION:  Use mobile or desktop?]_
- Desktop:  `min-width: 1025px`

**Decisions:**
- **Tablet behavior:** _[TO BE FILLED - mobile or desktop view?]_
- **Portrait/Landscape:** _[TO BE FILLED - different layouts? ]_

---

## Phase 2: Design Mobile Chat Interface

### Step 2.1: Create Mobile UI Mockup/Wireframe ⬜

**Design specifications:**

**Layout Structure:**
```
┌─────────────────────────┐
│  Header (fixed)         │ <- Conversation name, back button, menu
├─────────────────────────┤
│                         │
│   Messages Area         │ <- Scrollable, flex-grow
│   (scrollable)          │    - Received messages (left)
│                         │    - Sent messages (right)
│                         │    - Timestamps
│                         │
├─────────────────────────┤
│  Input Area (fixed)     │ <- Text input + Send button
└─────────────────────────┘
```

**Component Breakdown:**

- [ ] **Header (Fixed Top)**
  - Height: ~56px
  - Back button/arrow (left)
  - Conversation title/username (center)
  - Menu/options (right - optional)
  - Link to desktop view (in menu)
  
- [ ] **Messages Area (Scrollable)**
  - Full viewport height minus header and input
  - Auto-scroll to bottom on new message
  - Received messages: 
    - Align left
    - Gray background bubble
    - Avatar (optional, left side)
    - Timestamp below (small, gray)
  - Sent messages:
    - Align right
    - Brand color background bubble
    - White text
    - Timestamp below (small, gray)
  - Message bubbles:
    - Border-radius: ~18px
    - Padding: 12px 16px
    - Max-width: 75% of screen
    - Word wrap
    - Margin between messages:  8px
  
- [ ] **Input Area (Fixed Bottom)**
  - Height: auto (min ~56px)
  - Text input:
    - Flexible width
    - Border-radius: ~20px
    - Padding: 10px 16px
    - Light background
    - Placeholder text
    - Auto-grow for multi-line (max 4-5 lines)
  - Send button:
    - Icon or text
    - Brand color
    - Size: ~40px x 40px
    - Disabled state when empty
    - Active state on tap

**Color scheme:**
- Use existing theme colors
- Sent message bubble: Primary brand color
- Received message bubble:  Light gray (#E5E5EA for iOS-like, #DCF8C6 for WhatsApp-like)
- Text color: White on sent, dark on received
- Timestamps: Light gray (#8E8E93)

**Typography:**
- Message text: 16px (readable without zoom)
- Timestamps: 12px
- Header title: 17px (bold)

**Touch targets:**
- Minimum 44px x 44px for all interactive elements
- Send button: At least 44px x 44px
- Header buttons: At least 44px x 44px

**Design inspiration:**
- iMessage (iOS) - clean, minimal
- WhatsApp - functional, clear
- Telegram - feature-rich but clean

**Mockup location:** _[TO BE FILLED - create mockup image/Figma link]_

### Step 2.2: Plan Message Layout Algorithm ⬜

- [ ] Group messages by sender (consecutive messages from same sender)
- [ ] Add timestamps (show every N minutes, or on sender change)
- [ ] Handle date separators (Today, Yesterday, date)
- [ ] Handle long messages (word wrap, max-width)
- [ ] Handle links (make clickable, preserve security)
- [ ] Handle line breaks
- [ ] Plan for future:  images, emojis, attachments

**Algorithm pseudocode:**
```
For each message:
  - If sender different from previous:  add avatar, add spacing
  - If time gap > 10 minutes: show timestamp
  - If date different from previous: add date separator
  - If message from current user: align right, use sent style
  - Else: align left, use received style
  - Sanitize and render message content
```

### Step 2.3: Plan Interaction Behaviors ⬜

- [ ] **Sending a message:**
  - User types in input
  - Send button enables
  - User taps send or presses Enter
  - Message appears immediately in sent style (optimistic UI)
  - Input clears
  - Scroll to bottom
  - Show sending indicator (optional)
  - On success: mark as sent
  - On failure: show retry option

- [ ] **Receiving a message:**
  - New message arrives (WebSocket/SignalR/polling)
  - Append to messages area
  - If user at bottom: auto-scroll to show new message
  - If user scrolled up: show "new message" indicator (don't force scroll)
  - Play sound notification (optional)

- [ ] **Scrolling:**
  - Smooth scroll
  - Pull-to-refresh (optional, load older messages)
  - Scroll to bottom button (if user scrolled up)

- [ ] **Input behavior:**
  - Auto-focus on page load (optional)
  - Auto-grow text area as user types
  - Max height before scrolling inside textarea
  - Enter to send, Shift+Enter for new line (desktop behavior)
  - On mobile: Enter = new line, button = send

- [ ] **View switching:**
  - Link in header menu:  "Desktop View"
  - On tap: navigate to `/chat` (preserve conversation)
  - Set preference cookie

---

## Phase 3: Backend Implementation

### Step 3.1: Create Mobile Detection Service ⬜

Create `IMobileDetectionService` interface and implementation. 

**Requirements:**
- [ ] Detect mobile devices via User-Agent
- [ ] Detect tablet devices
- [ ] Return device type (mobile, tablet, desktop)
- [ ] Cache results per request (don't parse UA multiple times)

**Implementation options:**
- [ ] **Option 1:** Use existing library (recommended)
  - `Wangkanai.Detection` NuGet package
  - `UAParser` NuGet package
  - `51Degrees` (enterprise, overkill)
  
- [ ] **Option 2:** Custom regex-based detection
  - Maintain list of mobile/tablet UA patterns
  - Less accurate, more maintenance

**Chosen library:** _[TO BE FILLED]_
**Version:** _[TO BE FILLED]_
**File location:** _[TO BE FILLED]_

### Step 3.2: Create User Preference Service ⬜

Create service to remember user's view preference.

**Requirements:**
- [ ] Store user preference (mobile/desktop)
- [ ] Use cookie or localStorage
- [ ] Respect preference over auto-detection
- [ ] Cookie name: `ChatViewPreference`
- [ ] Values: `auto`, `mobile`, `desktop`
- [ ] Expiry: 90 days

**Implementation:**
- [ ] Create helper methods:  `SetChatViewPreference()`, `GetChatViewPreference()`
- [ ] Integrate with ChatController

**File location:** _[TO BE FILLED]_

### Step 3.3: Update Chat Controller ⬜

**Modify existing ChatController:**

- [ ] **Add new action:  `ChatMobile()`**
  - Route: `/chat-mobile`
  - Returns mobile-optimized view
  - Same functionality as desktop chat
  - Use same data/models
  
- [ ] **Modify existing `Chat()` action**
  - Check if user is on mobile device
  - Check user preference
  - If mobile AND preference is auto: redirect to `/chat-mobile`
  - If preference is `mobile`: redirect to `/chat-mobile`
  - Otherwise: show desktop view
  
- [ ] **Add action: `SetChatViewPreference(string preference)`**
  - Accepts: `mobile` or `desktop`
  - Sets cookie
  - Returns success

**Routing logic:**
```csharp
public IActionResult Chat()
{
    var preference = GetUserChatViewPreference(); // "auto", "mobile", "desktop"
    var isMobile = _mobileDetectionService.IsMobile();
    
    if (preference == "mobile" || (preference == "auto" && isMobile))
    {
        return RedirectToAction("ChatMobile");
    }
    
    return View("Chat"); // Desktop view
}

public IActionResult ChatMobile()
{
    // Same data loading as Chat()
    var model = LoadChatData();
    return View("ChatMobile", model);
}
```

**File location:** _[TO BE FILLED]_

### Step 3.4: Ensure API Endpoints Support Mobile ⬜

- [ ] Verify existing message send/receive endpoints work for mobile
- [ ] Ensure JSON responses are consistent
- [ ] Check CORS settings if applicable
- [ ] Verify real-time connection (SignalR/WebSockets) works on mobile
- [ ] Test with mobile User-Agents

**Endpoints to verify:**
- Send message: _[TO BE FILLED]_
- Receive messages: _[TO BE FILLED]_
- Load conversation: _[TO BE FILLED]_
- Load message history: _[TO BE FILLED]_

---

## Phase 4: Frontend Implementation - Mobile View

### Step 4.1: Create Mobile Chat View ⬜

Create new Razor view:  `Views/Chat/ChatMobile.cshtml`

**Structure:**
```html
<div class="chat-mobile-container">
    <!-- Header -->
    <header class="chat-mobile-header">
        <button class="back-button">←</button>
        <h1 class="conversation-title">@Model.ConversationName</h1>
        <div class="header-menu">
            <a href="/chat? view=desktop" class="view-toggle">Desktop View</a>
        </div>
    </header>
    
    <!-- Messages Area -->
    <div class="chat-mobile-messages" id="messageContainer">
        @foreach (var message in Model.Messages)
        {
            <!-- Render message partial -->
            <partial name="_MobileMessageBubble" model="message" />
        }
    </div>
    
    <!-- Input Area -->
    <div class="chat-mobile-input">
        <textarea 
            id="messageInput" 
            placeholder="Type a message..."
            rows="1"></textarea>
        <button id="sendButton" class="send-button" disabled>
            Send
        </button>
    </div>
</div>
```

**File location:** `Views/Chat/ChatMobile. cshtml`

### Step 4.2: Create Mobile Message Bubble Partial ⬜

Create partial view: `Views/Chat/_MobileMessageBubble.cshtml`

**Structure:**
```html
@model MessageViewModel

@{
    var isSent = Model.SenderId == ViewBag.CurrentUserId;
    var bubbleClass = isSent ? "message-bubble-sent" : "message-bubble-received";
    var containerClass = isSent ? "message-container-sent" : "message-container-received";
}

<div class="message-container @containerClass">
    @if (! isSent)
    {
        <img src="@Model.SenderAvatar" alt="" class="message-avatar" />
    }
    
    <div class="message-bubble @bubbleClass">
        <div class="message-content">@Model.Content</div>
    </div>
    
    @if (Model.ShowTimestamp)
    {
        <div class="message-timestamp">@Model.Timestamp. ToString("h:mm tt")</div>
    }
</div>
```

**File location:** `Views/Chat/_MobileMessageBubble.cshtml`

### Step 4.3: Create Mobile Chat JavaScript ⬜

Create JavaScript file: `wwwroot/js/chat-mobile.js`

**Functionality:**
- [ ] Auto-scroll to bottom on load
- [ ] Send message on button click
- [ ] Enable/disable send button based on input
- [ ] Auto-grow textarea as user types
- [ ] Listen for new messages (SignalR/WebSocket)
- [ ] Append new messages to DOM
- [ ] Handle send errors
- [ ] Smooth scroll animations
- [ ] "Scroll to bottom" button (when scrolled up)
- [ ] Handle Enter key (new line on mobile)

**Key functions:**
```javascript
// Auto-scroll to bottom
function scrollToBottom(smooth = true) { ... }

// Send message
function sendMessage() { ... }

// Receive message (from SignalR)
function onMessageReceived(message) { ... }

// Auto-grow textarea
function autoGrowTextarea() { ... }

// Enable/disable send button
function updateSendButton() { ... }

// Check if scrolled to bottom
function isScrolledToBottom() { ... }
```

**File location:** `wwwroot/js/chat-mobile.js`

### Step 4.4: Add View Toggle Links ⬜

- [ ] Add "Desktop View" link to mobile header
- [ ] Add "Mobile View" link to desktop chat (when on mobile device)
- [ ] Links should set preference cookie and navigate
- [ ] Preserve current conversation/state

**Desktop view addition:**
```html
<!-- In desktop Chat.cshtml, add for mobile users -->
@if (ViewBag.IsMobileDevice)
{
    <a href="/chat-mobile? view=mobile" class="mobile-view-link">
        📱 Switch to Mobile View
    </a>
}
```

**Implementation:**
- [ ] Update `Chat. cshtml` (desktop view)
- [ ] Update `ChatMobile.cshtml` (mobile view)
- [ ] Style toggle links appropriately for each view

---

## Phase 5: CSS & Styling

### Step 5.1: Create Mobile Chat CSS ⬜

Create CSS file: `wwwroot/css/chat-mobile.css`

**Requirements:**
- [ ] Full viewport height layout
- [ ] Flexbox for vertical arrangement
- [ ] Fixed header and input area
- [ ] Scrollable messages area
- [ ] Message bubble styling (sent/received)
- [ ] Touch-friendly sizing (44px minimum)
- [ ] Smooth animations
- [ ] Support for safe area insets (iPhone notch)
- [ ] Dark mode support (if site has it)

**CSS Structure:**
```css
/* Container - Full viewport */
.chat-mobile-container {
    display: flex;
    flex-direction: column;
    height: 100vh;
    height: 100dvh; /* Dynamic viewport height */
    overflow: hidden;
}

/* Header - Fixed */
.chat-mobile-header {
    position: sticky;
    top: 0;
    height: 56px;
    display: flex;
    align-items: center;
    padding: 0 16px;
    background:  var(--header-bg);
    border-bottom: 1px solid var(--border-color);
    z-index: 100;
}

/* Messages Area - Scrollable */
.chat-mobile-messages {
    flex: 1;
    overflow-y: auto;
    overflow-x: hidden;
    padding: 16px;
    display: flex;
    flex-direction: column;
    -webkit-overflow-scrolling: touch; /* Smooth scrolling iOS */
}

/* Input Area - Fixed */
.chat-mobile-input {
    position: sticky;
    bottom: 0;
    display: flex;
    align-items: flex-end;
    padding: 12px 16px;
    background: var(--input-bg);
    border-top: 1px solid var(--border-color);
    gap: 8px;
    padding-bottom: calc(12px + env(safe-area-inset-bottom)); /* iPhone notch */
}

/* Message Bubbles */
.message-container {
    display: flex;
    margin-bottom: 12px;
    gap: 8px;
}

.message-container-sent {
    justify-content: flex-end;
}

.message-container-received {
    justify-content: flex-start;
}

.message-bubble {
    max-width: 75%;
    padding: 12px 16px;
    border-radius: 18px;
    word-wrap: break-word;
    font-size: 16px;
    line-height: 1.4;
}

.message-bubble-sent {
    background: var(--brand-color);
    color: white;
    border-bottom-right-radius: 4px;
}

.message-bubble-received {
    background:  #E5E5EA;
    color: #000;
    border-bottom-left-radius: 4px;
}

/* Textarea */
.chat-mobile-input textarea {
    flex: 1;
    border: 1px solid var(--border-color);
    border-radius: 20px;
    padding: 10px 16px;
    font-size: 16px;
    resize: none;
    max-height: 120px;
    font-family: inherit;
}

/* Send Button */
.send-button {
    width: 44px;
    height: 44px;
    border-radius: 50%;
    background: var(--brand-color);
    color: white;
    border: none;
    font-weight: 600;
    flex-shrink: 0;
}

.send-button:disabled {
    opacity: 0.5;
}

/* Timestamps */
.message-timestamp {
    font-size: 12px;
    color: #8E8E93;
    margin-top: 4px;
    text-align: center;
}

/* Avatar */
.message-avatar {
    width: 32px;
    height: 32px;
    border-radius:  50%;
    object-fit: cover;
    flex-shrink: 0;
}

/* Dark mode (if applicable) */
@media (prefers-color-scheme: dark) {
    .message-bubble-received {
        background: #3A3A3C;
        color: white;
    }
}
```

**File location:** `wwwroot/css/chat-mobile.css`

### Step 5.2: Handle Safe Areas (iOS Notch) ⬜

- [ ] Use `env(safe-area-inset-*)` for padding
- [ ] Apply to header top
- [ ] Apply to input bottom
- [ ] Test on iPhone X and newer

**CSS additions:**
```css
.chat-mobile-header {
    padding-top: calc(8px + env(safe-area-inset-top));
}

.chat-mobile-input {
    padding-bottom: calc(12px + env(safe-area-inset-bottom));
}
```

### Step 5.3: Add Loading & Empty States ⬜

- [ ] Loading spinner while messages load
- [ ] Empty state when no messages
- [ ] Connection lost state
- [ ] Message sending state

**File location:** Add to `chat-mobile.css`

### Step 5.4: Optimize for Performance ⬜

- [ ] Use CSS transforms for animations (GPU accelerated)
- [ ] Minimize repaints/reflows
- [ ] Use `will-change` sparingly
- [ ] Optimize message rendering (virtual scrolling for 1000+ messages)
- [ ] Lazy load images in messages

---

## Phase 6: Desktop View Updates

### Step 6.1: Add Mobile View Link to Desktop Chat ⬜

- [ ] Detect if user is on mobile device (even if viewing desktop version)
- [ ] Show subtle link/button:  "Switch to Mobile View"
- [ ] Place in header or settings menu
- [ ] Style to match desktop UI

**File location:** `Views/Chat/Chat.cshtml` (existing desktop view)

### Step 6.2: Handle Preference Storage ⬜

- [ ] When user clicks "Desktop View" from mobile:  set preference to `desktop`
- [ ] When user clicks "Mobile View" from desktop: set preference to `mobile`
- [ ] Add option to reset to `auto` (follow device type)

**Cookie structure:**
```
Name: ChatViewPreference
Value:  auto | mobile | desktop
Expires: 90 days
Path: /chat
```

---

## Phase 7: Testing & Quality Assurance

### Step 7.1: Cross-Device Testing ⬜

**Test on real devices:**
- [ ] iPhone (iOS 15+) - Safari
- [ ] iPhone (iOS 15+) - Chrome
- [ ] Android phone - Chrome
- [ ] Android phone - Firefox
- [ ] Android phone - Samsung Internet
- [ ] Tablet (iPad) - Decision:  mobile or desktop view? 
- [ ] Tablet (Android) - Decision: mobile or desktop view?

**Test responsive breakpoints:**
- [ ] 320px (iPhone SE)
- [ ] 375px (iPhone 12/13)
- [ ] 390px (iPhone 14)
- [ ] 414px (iPhone Plus models)
- [ ] 768px (iPad portrait)

### Step 7.2: Functionality Testing ⬜

- [ ] Send message - appears immediately (optimistic UI)
- [ ] Receive message - appears in real-time
- [ ] Scroll to bottom on new message (when already at bottom)
- [ ] Don't auto-scroll if user scrolled up
- [ ] Message bubbles render correctly (sent vs received)
- [ ] Timestamps display correctly
- [ ] Avatars display correctly
- [ ] Long messages wrap properly
- [ ] Links in messages are clickable
- [ ] Textarea auto-grows up to max height
- [ ] Send button enables/disables correctly
- [ ] Enter key behavior (new line)
- [ ] View toggle works (mobile ↔ desktop)
- [ ] Preference is remembered
- [ ] Auto-detection works correctly
- [ ] Manual override works

### Step 7.3: Performance Testing ⬜

- [ ] Test with 100 messages (should load quickly)
- [ ] Test with 500 messages (acceptable performance)
- [ ] Test with 1000+ messages (may need virtual scrolling)
- [ ] Measure time to interactive (TTI)
- [ ] Test on slow 3G connection
- [ ] Check bundle sizes (CSS/JS)
- [ ] Verify images are optimized
- [ ] Test scroll performance (60fps)

**Performance targets:**
- Initial load:  < 3 seconds on 3G
- Time to interactive: < 5 seconds on 3G
- Scroll:  60fps
- Send message response: < 100ms (optimistic UI)

### Step 7.4:  Accessibility Testing ⬜

- [ ] Keyboard navigation (external keyboard on mobile)
- [ ] Screen reader support (VoiceOver, TalkBack)
- [ ] Touch target sizes (minimum 44x44px)
- [ ] Color contrast ratios (WCAG AA)
- [ ] Focus indicators
- [ ] ARIA labels where needed
- [ ] Semantic HTML

### Step 7.5: Edge Cases & Error Handling ⬜

- [ ] No internet connection (show error message)
- [ ] Server error (show retry option)
- [ ] Message send failure (show retry)
- [ ] WebSocket/SignalR disconnect (auto-reconnect)
- [ ] Very long messages (thousands of characters)
- [ ] Empty messages (prevent sending)
- [ ] Special characters in messages
- [ ] Emoji rendering
- [ ] Landscape orientation
- [ ] Virtual keyboard covering input
- [ ] iOS Safari bottom bar appearing/disappearing

### Step 7.6: User Agent Detection Testing ⬜

- [ ] Test with real mobile User-Agents
- [ ] Test with desktop User-Agents
- [ ] Test with tablet User-Agents
- [ ] Test with uncommon browsers
- [ ] Verify fallback behavior if detection fails

---

## Phase 8: Polish & Optimization

### Step 8.1: Animation & Micro-interactions ⬜

- [ ] Message send animation (bubble appears with slide/fade)
- [ ] Smooth scroll to bottom
- [ ] Send button press animation
- [ ] Typing indicator (optional)
- [ ] Pull-to-refresh animation (optional)
- [ ] Loading spinner
- [ ] Transitions between states

**Keep animations subtle and fast:**
- Duration: 200-300ms
- Easing: ease-out or ease-in-out
- Use transform and opacity (GPU accelerated)

### Step 8.2: Offline Support (Optional) ⬜

- [ ] Service Worker for offline page
- [ ] Cache static assets (CSS, JS, images)
- [ ] Show offline indicator
- [ ] Queue messages while offline
- [ ] Send when connection restored

**Priority:** LOW (Phase 2 feature)

### Step 8.3: Progressive Web App Features (Optional) ⬜

- [ ] Add manifest.json
- [ ] Add app icons
- [ ] Add to home screen prompt
- [ ] Standalone display mode

**Priority:** LOW (Phase 2 feature)

### Step 8.4: Optimize Images & Assets ⬜

- [ ] Compress CSS/JS
- [ ] Optimize avatar images
- [ ] Use WebP for images (with fallback)
- [ ] Lazy load images
- [ ] Use CDN if applicable

---

## Phase 9: Documentation & Deployment

### Step 9.1: Update Documentation ⬜

- [ ] Document new routes (`/chat`, `/chat-mobile`)
- [ ] Document mobile detection service
- [ ] Document preference system
- [ ] Update user help/FAQ
- [ ] Add screenshots to docs

**Documentation location:** _[TO BE FILLED]_

### Step 9.2: Create User Guide ⬜

- [ ] How to switch between mobile and desktop views
- [ ] Explain auto-detection
- [ ] How to reset preference
- [ ] Troubleshooting common issues

### Step 9.3: Staging Deployment ⬜

- [ ] Deploy to staging environment
- [ ] Test all functionality in staging
- [ ] Test with real mobile devices on staging
- [ ] Performance test on staging
- [ ] Get feedback from test users

### Step 9.4: Production Deployment ⬜

- [ ] Create deployment plan
- [ ] Backup database
- [ ] Deploy to production
- [ ] Smoke test critical paths
- [ ] Monitor error logs
- [ ] Monitor performance metrics
- [ ] Gather user feedback

### Step 9.5: Post-Launch Monitoring ⬜

- [ ] Monitor mobile vs desktop usage
- [ ] Track view preference changes
- [ ] Monitor error rates
- [ ] Collect user feedback
- [ ] Identify areas for improvement
- [ ] Plan Phase 2 features

---

## Technical Specifications

### Routes

| Route | Method | Description | Access |
|-------|--------|-------------|--------|
| `/chat` | GET | Desktop chat view (auto-redirects mobile) | Authenticated |
| `/chat-mobile` | GET | Mobile-optimized chat view | Authenticated |
| `/api/chat/set-preference` | POST | Set view preference | Authenticated |
| `/api/chat/send` | POST | Send message (existing) | Authenticated |
| `/api/chat/messages` | GET | Get messages (existing) | Authenticated |

### Cookie Specification

```
Name: ChatViewPreference
Values: 
  - "auto" (default) - Follow device detection
  - "mobile" - Always use mobile view
  - "desktop" - Always use desktop view
Expires: 90 days
Path: /chat
HttpOnly: false (needs JS access)
Secure: true (HTTPS only)
SameSite: Lax
```

### Mobile Detection

**User-Agent patterns to detect:**

Mobile: 
- iPhone
- Android (phone)
- Windows Phone
- iPod

Tablet (decision needed):
- iPad
- Android (tablet)

**Recommended library:** `Wangkanai. Detection`
```csharp
services.AddDetection();
```

### Viewport Meta Tag

```html
<meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no">
```

### CSS Custom Properties

```css
:root {
    --brand-color: #007AFF; /* Or existing brand color */
    --header-bg: #FFFFFF;
    --input-bg: #F2F2F7;
    --border-color:  #D1D1D6;
    --received-bubble-bg: #E5E5EA;
    --sent-bubble-bg: var(--brand-color);
    --text-primary: #000000;
    --text-secondary: #8E8E93;
}

@media (prefers-color-scheme: dark) {
    :root {
        --header-bg: #1C1C1E;
        --input-bg: #2C2C2E;
        --border-color: #38383A;
        --received-bubble-bg: #3A3A3C;
        --text-primary: #FFFFFF;
        --text-secondary: #8E8E93;
    }
}
```

---

## Future Enhancements (Phase 2)

- [ ] Typing indicators ("User is typing...")
- [ ] Read receipts (seen/delivered status)
- [ ] Message reactions (emoji reactions)
- [ ] Swipe to reply
- [ ] Message search
- [ ] Image/file uploads in chat
- [ ] Voice messages
- [ ] Video messages
- [ ] Group chat support
- [ ] Push notifications
- [ ] Unread message count badge
- [ ] Message editing
- [ ] Message deletion
- [ ] Forward messages
- [ ] Share messages
- [ ] Dark mode toggle
- [ ] Custom themes
- [ ] Chat wallpapers
- [ ] Archived conversations
- [ ] Pinned conversations
- [ ] Message export

---

## Notes & Discoveries

### [2026-01-05] - Initial Document Creation
- Document created by AI agent
- Repository: JaredCH/myface
- Current chat page has poor mobile UX
- Creating parallel mobile view while preserving desktop experience
- Using dual-route approach:  `/chat` and `/chat-mobile`

### Agent Instructions

**TO FUTURE AI AGENTS WORKING ON THIS SPRINT:**

1. **ALWAYS read this document first** before making changes
2. **DO NOT overwrite** existing content in this document
3. **CHECK OFF items** as you complete them using [x] instead of [ ]
4. **ADD NOTES** in the "Notes & Discoveries" section with: 
   - Date
   - What you worked on
   - File locations you created/modified
   - Screenshots (before/after)
   - Any issues encountered
   - Performance metrics
   - Test results
5. **UPDATE** the "File locations" placeholders as you create files
6. **TAKE SCREENSHOTS** of before/after for comparison
7. **TEST ON REAL DEVICES** - emulators are not enough
8. **IF YOU ENCOUNTER ISSUES**, add a note and ask the user before proceeding

### Notes Section (Add new entries below)

---

<!-- 
TEMPLATE FOR NEW NOTES:

### [YYYY-MM-DD] - [Your Work Summary]
- **Phase/Step:** [e.g., Phase 4, Step 4. 1]
- **Files Modified/Created:**
  - path/to/file1.cshtml
  - path/to/file2.css
- **Completed Tasks:**
  - [x] Task 1
  - [x] Task 2
- **Screenshots:** [links to before/after images]
- **Test Results:** [pass/fail, performance metrics]
- **Issues Encountered:** [description]
- **Decisions Made:** [any deviations from plan]
- **Next Steps:** [what should be done next]

---

-->

---

## Summary Progress Tracker

- [ ] Phase 1: Discovery & Analysis (0/3 steps)
- [ ] Phase 2: Design Mobile Interface (0/3 steps)
- [ ] Phase 3: Backend Implementation (0/4 steps)
- [ ] Phase 4: Frontend - Mobile View (0/4 steps)
- [ ] Phase 5: CSS & Styling (0/4 steps)
- [ ] Phase 6: Desktop View Updates (0/2 steps)
- [ ] Phase 7: Testing & QA (0/6 steps)
- [ ] Phase 8: Polish & Optimization (0/4 steps)
- [ ] Phase 9: Documentation & Deployment (0/5 steps)

**Total Progress:  0/35 major steps completed**

---

## Quick Reference Links

- **Mobile Detection Library:** https://github.com/wangkanai/Detection
- **CSS Safe Area Insets:** https://webkit.org/blog/7929/designing-websites-for-iphone-x/
- **Touch Target Size Guidelines:** https://www.w3.org/WAI/WCAG21/Understanding/target-size. html
- **iOS Design Guidelines:** https://developer.apple.com/design/human-interface-guidelines/
- **Material Design (Android):** https://m3.material.io/

---

## Design Inspiration Examples

- **iMessage (iOS):** Clean, minimal, excellent touch interactions
- **WhatsApp:** Functional, clear visual hierarchy, good performance
- **Telegram:** Feature-rich while maintaining simplicity
- **Facebook Messenger:** Smooth animations, polished feel
- **Signal:** Privacy-focused, clean design

---

## Testing Checklist (Quick Reference)

**Must test on:**
- [ ] iPhone (Safari)
- [ ] Android (Chrome)
- [ ] Slow 3G network
- [ ] Portrait orientation
- [ ] Landscape orientation
- [ ] With virtual keyboard open
- [ ] 100+ messages loaded
- [ ] Send/receive during poor connection
- [ ] Switch between views
- [ ] Preference persistence

---

**End of Document** - Last updated: 2026-01-05

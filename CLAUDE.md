Yaqoot Perfume Store

Document Priority

When working on this project, follow instructions in this order of priority:



The current user task.

This CLAUDE.md document.

Project Instructions.



If two instructions conflict, always follow the higher priority instruction.

1\. Project Overview

Project Purpose

Yaqoot Perfume Store is a production-oriented e-commerce web application for selling luxury perfumes.

This is a real-world project under active development and should always be treated as a production application rather than a prototype or demo.

Every implementation should prioritize maintainability, scalability, performance, accessibility, security, and clean architecture.



Technology Stack

Backend



ASP.NET Core MVC



Frontend



Razor Views (.cshtml)

HTML5

CSS3

JavaScript

Bootstrap (Existing Project)



Database



PostgreSQL

Hosted on Neon



Hosting



Render



Source Control



GitHub



Media Storage



Cloudinary





Project Architecture

This project follows the existing ASP.NET Core MVC architecture.

Presentation Layer



Razor Views

CSS

JavaScript



Business Layer



Existing Services



Data Layer



Entity Framework Core

PostgreSQL (Neon)



Do not replace the existing architecture unless explicitly instructed.



Design Goal

The website should represent a premium luxury perfume brand.

The user interface should feel comparable to modern high-end e-commerce websites while remaining simple, elegant, fast, and highly maintainable.

The existing UI is NOT the design reference.

Treat the existing pages only as a source of functionality.

When redesigning a page, you are encouraged to build a completely new user interface while preserving all functionality and dynamic data flow.



Dynamic Data

Business data must always remain dynamic.

The application currently relies on PostgreSQL (Neon) as the source of truth.

Never replace dynamic content with static content.

Never invent or hardcode:



Products

Categories

Prices

Discounts

Images

Reviews

Statistics

Users

Orders

Business information



If required data is unavailable from the current backend:



Do not fake data.

Keep the UI prepared for dynamic rendering.

Clearly report the missing backend requirement.





Media Storage

All user-uploaded images should be stored using Cloudinary.

PostgreSQL should store only image metadata such as public IDs or URLs.

Do not assume uploaded images exist inside the project directory or under wwwroot.



Development Principles

Every change should aim to improve:



Code Quality

Readability

Maintainability

Scalability

Performance

Accessibility

Consistency



Avoid technical debt whenever possible.

Always implement production-quality solutions.

2\. Design System

Design Philosophy

Yaqoot is a premium luxury perfume brand.

Every page should communicate elegance, exclusivity, sophistication, and trust.

The objective is not simply to create beautiful interfaces, but to deliver a premium shopping experience.

The design should feel timeless rather than trendy.

Always prioritize quality over visual complexity.



Brand Identity

The logo is the foundation of the project's visual identity.

The logo must never be redesigned, replaced, ignored, or visually competed with.

Every page should immediately feel connected to the brand, even when the logo is not visible.

Brand consistency always takes priority over visual creativity.



Application Experience

The project contains two distinct user experiences:



Public Storefront

Administrative Dashboard



Each should have its own visual style while remaining consistent with the Yaqoot brand identity.

The Public Storefront should emphasize:



Luxury

Elegance

Emotional shopping experience

Product presentation



The Administrative Dashboard should emphasize:



Productivity

Clarity

Simplicity

Fast workflows

Data management



Do not design the administrative dashboard like the storefront.

Do not design the storefront like an administrative system.



Visual Identity

The current website is NOT the visual reference.

Treat the existing UI only as a source of functionality.

When redesigning pages, build a completely new interface while preserving the established brand identity.

All pages must appear as though they were designed by the same design team.

Never mix different visual styles across the application.



Color System

The logo is the single source of truth for the project's color palette.

Derive the primary colors directly from the logo.

Secondary, neutral, and supporting colors should complement the logo without competing with it.

Maintain a consistent color palette across the entire application.

Avoid introducing colors that conflict with the brand identity.

Avoid:



Random accent colors

Neon colors

Overly saturated colors

Cartoon-like color schemes

Inconsistent color usage



Color usage should always reinforce the luxury identity of the brand.



Typography

Typography should feel premium, elegant, and highly readable.

Maintain a clear visual hierarchy throughout the application.

Use consistent:



Font families

Font sizes

Font weights

Line heights

Letter spacing



Avoid excessive typography variations.

Readability always has priority over decorative typography.



Layout

Layouts should feel spacious, balanced, and premium.

Use generous whitespace.

Guide the user's attention naturally through visual hierarchy.

Avoid cluttered interfaces.

Each section should have a clear purpose.

Content density should remain comfortable on every screen size.



Components

All reusable components must follow a unified design language.

This includes:



Buttons

Cards

Forms

Inputs

Selects

Navigation

Menus

Dropdowns

Tables

Badges

Alerts

Modals

Pagination

Product Cards



Do not create multiple visual styles for the same component unless there is a functional requirement.

Reuse existing component patterns whenever possible.



Product Presentation

Products are the primary focus of the website.

Product cards should emphasize:



Product image

Product name

Price

Important actions



Do not allow decorative elements to distract from the products.

Maintain consistent image proportions throughout the application.



Icons

Use icons only when they improve usability.

Icons should remain visually consistent across the application.

Avoid decorative icon overload.



Animations

Animations should feel elegant and intentional.

Prefer:



Smooth transitions

Soft hover effects

Gentle fades

Small micro-interactions



Avoid:



Flashy animations

Excessive motion

Distracting effects



Performance is always more important than animations.



Responsive Design

Every page must provide an excellent experience on:



Mobile

Tablet

Laptop

Desktop



Prevent:



Horizontal scrolling

Overflow

Broken layouts

Overlapping elements

Inconsistent spacing



Design mobile-first whenever practical.



Accessibility

Maintain reasonable accessibility throughout the application.

Prioritize:



Semantic HTML

Proper heading hierarchy

Keyboard accessibility

Readable typography

Adequate color contrast

Clear focus indicators



Accessibility improvements should integrate naturally into the design.



Design Consistency

Before designing any page:

Review the existing design language established by previously completed pages.

New pages should extend the existing design system rather than introducing a new one.

The final application should feel like one cohesive product rather than a collection of independently designed pages.

Consistency is more important than individual page creativity.



Frontend Architecture



General Principles

The frontend must remain clean, modular, maintainable, and easy for a development team to extend.

Follow the existing ASP.NET Core MVC architecture.

Modify only the files required for the requested task.

Avoid unnecessary changes.



Razor Views

Razor Views are responsible only for presentation.

Views may contain:



HTML

Razor syntax

Tag Helpers



Views must NOT contain:



Business logic

Database logic

Service logic

Complex C# logic



Keep Razor code clean and readable.



Razor Preservation

When redesigning a page:

You may completely rebuild the HTML structure.

However, preserve:



@model

Model binding

ViewModel binding

Razor directives

Razor loops

Razor conditions

Partial rendering

View Components

Tag Helpers

Routing

Form submission

Existing functionality



Preserve the data flow.

Do not replace dynamic rendering with static HTML.



HTML Structure

The previous HTML structure is NOT the design reference.

When redesigning a page:



Build a completely new HTML structure.

Use semantic HTML.

Improve accessibility.

Keep the markup clean.

Remove unnecessary wrappers.

Avoid deeply nested elements.



Do not preserve poor HTML simply because it already exists.



CSS Organization

Do NOT reuse legacy page-specific CSS.

Each redesigned page should receive its own dedicated stylesheet.

Example:

wwwroot/css/home/index.css

wwwroot/css/products/index.css

wwwroot/css/products/details.css

Each stylesheet should contain only the styles for its corresponding page.

Avoid large global stylesheets.

Avoid page-specific styles inside shared CSS files.



JavaScript Organization

Do NOT reuse legacy page-specific JavaScript.

Each page should have its own JavaScript file when custom functionality is required.

Example:

wwwroot/js/home/index.js

wwwroot/js/products/index.js

wwwroot/js/products/details.js

Do not create JavaScript files unless the page genuinely requires custom behavior.

Avoid inline JavaScript whenever possible.



Shared Resources

Shared resources should contain only functionality that is genuinely shared across multiple pages.

Examples:



Global variables

Theme utilities

Shared animations

Common helper functions

Site-wide behaviors



Never place page-specific logic inside shared files.



Bootstrap

Bootstrap may remain as the underlying layout framework if already part of the project.

Do NOT rely on Bootstrap's default visual appearance.

Create a custom premium interface instead of a default Bootstrap-looking website.

Bootstrap should provide structure, not branding.



Layout

\_Layout.cshtml represents the shared application shell.

It may be modified only when the requested feature requires changes that affect the entire website.

Examples:



Navigation

Footer

Shared assets

Global scripts

Global styles



Do not modify the Layout for page-specific requirements.



Partial Views

Use Partial Views for reusable UI sections.

Do not duplicate repeated markup across multiple pages.

If a component appears in multiple locations, extract it into a reusable Partial View when appropriate.



View Components

Use existing View Components whenever applicable.

Do not replace them with duplicated HTML.

Create new View Components only when they provide clear architectural benefits.



CSS Naming

Use meaningful class names.

Prefer descriptive names based on purpose rather than appearance.

Good:

product-card

hero-section

featured-products

newsletter-form

Avoid:

box1

left

red-button

style2

test



File Organization

Organize frontend assets consistently.

Example:

wwwroot/

css/

home/

index.css

products/

index.css

details.css

cart/

index.css

js/

home/

index.js

products/

index.js

details.js

cart/

index.js

Keep related files together.

Maintain a predictable project structure.



Code Quality

Write production-quality frontend code.

Prioritize:



Readability

Maintainability

Scalability

Simplicity



Avoid unnecessary abstraction.

Avoid duplicated code.

Avoid dead code.

Avoid commented-out code.



Frontend Performance

Write efficient HTML, CSS, and JavaScript.

Avoid:



Unnecessary DOM elements

Unused CSS

Unused JavaScript

Large dependencies

Duplicate styles



Performance should never be sacrificed for visual effects.



Maintainability

Assume multiple developers are working on this repository.

Every change should be:



Easy to review

Easy to understand

Easy to maintain

Easy to extend



Keep changes isolated to the requested task whenever possible.

4\. Data \& Business Rules

Source of Truth

The application's business data is stored in PostgreSQL hosted on Neon.

PostgreSQL is the single source of truth for all business information.

Never replace dynamic data with hardcoded values.

Never bypass the existing data flow.



Dynamic Rendering

Business data must always be rendered dynamically using the existing ASP.NET Core MVC architecture.

Preserve:



Models

ViewModels

Razor rendering

Existing data flow



Do not replace dynamic rendering with static HTML.



Never Hardcode Business Data

Never hardcode:



Products

Categories

Brands

Prices

Discounts

Inventory

Product descriptions

Images

Reviews

Ratings

Orders

Customers

User information

Statistics

Business text



Always use the existing backend data.



Unknown Data

Never assume data exists.

Never invent business information.

Never generate fake:



Products

Categories

Prices

Images

Reviews

Inventory

Promotions



If the backend does not provide the required data:



Do not fake it.

Keep the UI prepared for future dynamic rendering.

Clearly report which backend data is required.





Product Images

Product images are uploaded by administrators.

Uploaded images are stored in Cloudinary.

The database stores only image references such as URLs or public IDs.

Never assume uploaded images exist inside the project directory.

Never generate placeholder product images unless explicitly requested.



Product Information

Display only information provided by the backend.

Do not create additional product attributes.

Do not infer missing information.

If a field is unavailable, gracefully adapt the UI instead of inventing content.



Business Flow

Preserve the existing business workflow.

Do not modify:



Product flow

Shopping flow

Cart flow

Checkout flow

Authentication flow

Authorization flow

Admin workflow



unless explicitly instructed.



Forms

Preserve all existing forms.

Preserve:



Form actions

Model binding

Validation

Anti-forgery tokens

Existing submission behavior



Do not simplify forms by removing existing functionality.



Routing

Preserve all existing routing.

Do not change:



Controllers

Actions

Routes

Route parameters

Navigation flow



unless explicitly requested.



Backend Protection

Do not modify:



Controllers

Services

Repositories

Entity Framework

Database schema

Business logic



unless explicitly instructed.

If frontend improvements require backend changes, report the limitation instead of implementing assumptions.



Database Integrity

Never perform changes that may compromise database integrity.

Do not:



Modify database structure.

Change relationships.

Rename entities.

Remove existing fields.



unless explicitly requested.



Data Consistency

Always preserve consistency between:



UI

Razor Views

ViewModels

Controllers

Database



The frontend should always represent the current backend state.



Error Handling

If required business data is unavailable:

Do not hide the problem.

Do not fabricate data.

Instead:



Render the page safely.

Preserve existing functionality.

Clearly report the missing backend requirement.





Future Scalability

Design pages assuming the amount of business data will grow.

Avoid layouts that only work with a small number of products.

Design components to support:



Large catalogs

Multiple categories

Pagination

Filtering

Searching

Future expansion



without requiring redesign.

Existing Code First

Before creating any new code:

Search for an existing implementation.

If a reusable solution already exists, reuse or extend it.

Avoid creating duplicate components, duplicate utilities, duplicate styles, or duplicate logic.

Application Access Flow

The application supports guest browsing.

Visitors should be able to access public pages without authentication, including:



Home

Products

Product Details

Categories

Search

About

Contact



Authentication should only be required for protected user features such as:



Shopping Cart

Checkout

User Profile

Order History

Wishlist

Product Reviews (if authentication is required)





Administrative Area

The administrative dashboard must remain fully protected.

Preserve all existing:



Authentication

Authorization

Roles

Policies

Route protection



Never expose administrative functionality to unauthenticated or unauthorized users.

The administrative user interface may be redesigned when explicitly requested.

Redesigning the administrative interface must preserve:



Authentication

Authorization

Existing functionality

Existing business workflow



Only the presentation layer should change unless explicitly instructed otherwise.

5\. Development Workflow

Working Philosophy

Treat every task as part of a long-term production project.

Prioritize:



Maintainability

Scalability

Readability

Consistency

Performance



Avoid unnecessary complexity and technical debt.

Implement only what is required.



Task Scope

Every task is limited to the requested scope.

Modify only the files necessary to complete the requested work.

Do not modify unrelated files.

Do not perform repository-wide refactoring unless explicitly instructed.

Keep every task focused, isolated, and easy to review.



Before Starting

Review PROJECT\_STATUS.md to understand the current project state.

Before making any changes:



Understand the requested task.

Read only the files required to complete the task.

Identify the current functionality.

Identify the existing Razor rendering.

Understand the current data flow.



Do not scan the entire repository.

Do not inspect unrelated folders or files.



Existing Code First

Before creating new code:

Search for an existing implementation.

Prefer extending existing components over creating new ones.

Reuse existing:



Partial Views

View Components

Helpers

Utilities

Shared Components



Avoid duplicated implementations.



Implementation Strategy

When redesigning a page:



Build a completely new presentation layer.

Preserve functionality.

Preserve Razor rendering.

Preserve backend integration.

Preserve routing.

Preserve dynamic data.



Do not preserve poor HTML simply because it already exists.



File Changes

Modify the minimum number of files necessary.

Keep changes localized.

Avoid modifying shared resources unless the requested feature affects the entire application.

Do not rewrite files that only require small modifications.



Shared Resources

Shared resources should only contain functionality that is genuinely shared.

Examples include:



\_Layout.cshtml

Shared Partial Views

Shared CSS

Shared JavaScript



Do not place page-specific code inside shared resources.



Token Efficiency

Optimize every task for minimal token usage.

Always:



Read the minimum number of files required.

Avoid repository-wide analysis.

Avoid repeated file inspection.

Avoid unnecessary explanations.

Avoid unnecessary code generation.

Avoid rewriting code without a clear benefit.



Focus on implementation instead of analysis.



Communication

Assume reasonable defaults whenever possible.

Only ask questions when the answer directly affects:



Functionality

Architecture

Business Rules



Otherwise, proceed with implementation.



Backend Limitations

If frontend improvements require backend support:

Do not invent backend functionality.

Do not fake business data.

Implement everything that can be completed safely on the frontend.

Clearly report:



Missing backend functionality.

Missing backend data.

Required backend changes.





Code Review Mindset

Write code as though another developer will immediately review it.

Every change should be:



Easy to understand

Easy to review

Easy to maintain

Easy to extend



Prefer simple, clean solutions.



Git Workflow

Keep every task suitable for a small Pull Request.

Avoid mixing unrelated changes.

Keep commits logically isolated.

Maintain a clean repository history.



Task Completion

After completing a task, return only:



Modified files.

New files (if any).

Short implementation summary.

Backend limitations (if any).



Do not include unnecessary explanations or lengthy descriptions unless explicitly requested.

UI Redesign Authority

When explicitly requested to redesign a page:



Treat the existing UI only as a source of functionality.

You are encouraged to completely rebuild the presentation layer.

You may replace the existing HTML structure.

You may replace page-specific CSS.

You may replace page-specific JavaScript.

Preserve functionality, data flow, routing, and backend integration.



Do not attempt to preserve the previous visual design unless explicitly instructed.

Missing Views and Components

If a requested feature cannot be implemented properly because a required View, Partial View, View Component, CSS file, or JavaScript file does not exist, create it only when it is necessary to deliver a complete production-ready implementation.

Do not create unnecessary files or components.

Every newly created file must:



Integrate naturally with the existing ASP.NET Core MVC architecture.

Follow the project's established folder structure and naming conventions.

Be reusable where appropriate.

Preserve the existing business logic and application flow.



When multiple implementation options are possible, choose the solution that is the simplest, most maintainable, and most consistent with the existing project.



Project Status



PROJECT\_STATUS.md represents the authoritative development status of the project.



Always read PROJECT\_STATUS.md before starting any task.



When a completed task changes the project state, automatically update PROJECT\_STATUS.md if required.



Update PROJECT\_STATUS.md only when one or more of the following occurs:



\- A Phase starts.

\- A Phase is completed.

\- A major feature is completed.

\- A project version changes.

\- A reusable component is introduced.

\- A significant architectural decision is made.

\- A new external service or dependency is added.

\- A development milestone is reached.



Only modify the affected sections.



Do not rewrite the entire document.



Do not record:



\- Minor UI adjustments.

\- CSS refinements.

\- JavaScript refinements.

\- Bug fixes.

\- Small refactoring.

\- Temporary implementation details.



Keep PROJECT\_STATUS.md concise, factual, and synchronized with the actual project state.



Task Completion Report



At the end of every completed task, always provide a development report.



Never omit this report.



The report must contain exactly the following sections.



Task Summary



Modified Files



New Files



Deleted Files



Architectural Changes

Backend Limitations



Reusable Components



PROJECT\_STATUS.md



Suggested Git Commit Message



Requirements:



\- Task Summary must contain 2–5 concise sentences.

\- List every modified file.

\- List every newly created file.

\- List every deleted file.

\- If no files were created or deleted, explicitly write "None".

\- Describe architectural changes only if applicable.

\- List newly introduced reusable components if applicable.

\- If PROJECT\_STATUS.md was updated, briefly describe the changes.

\- Otherwise explicitly write "No update required."

Provide exactly one concise Git commit message following the Conventional Commits specification.



Do not include markdown formatting.



Return only the commit message.



Example:



feat(home): redesign homepage



6\. Performance \& Optimization

Performance Philosophy

Performance is a core requirement of this project.

Every implementation should aim to deliver a fast, responsive, and efficient user experience.

Never sacrifice performance for unnecessary visual effects.



HTML Performance

Write clean and semantic HTML.

Avoid:



Unnecessary wrapper elements.

Deeply nested structures.

Duplicate markup.

Empty elements.



Keep the DOM as small and simple as possible.



CSS Performance

Write efficient and maintainable CSS.

Avoid:



Unused selectors.

Duplicate rules.

Overly specific selectors.

Excessive nesting.

Large page-wide stylesheets.



Prefer reusable utility patterns when appropriate.

Only load CSS required for the current page.



JavaScript Performance

JavaScript should only be used when necessary.

Before writing JavaScript, consider whether the same result can be achieved using:



HTML

CSS



Avoid:



Unnecessary event listeners.

Duplicate logic.

Blocking scripts.

Global variables.

Heavy DOM manipulation.



Keep JavaScript modular and page-specific.



Asset Loading

Load only the resources required by the current page.

Avoid loading:



Unused JavaScript files.

Unused CSS files.

Unused fonts.

Unused icons.



Do not include page-specific assets globally.



Images

Images are critical to the shopping experience.

Optimize images for web delivery.

Prefer appropriately sized images.

Do not use unnecessarily large image files.

Use lazy loading where appropriate.

Maintain image quality while minimizing file size.



Animations

Animations should be lightweight.

Prefer CSS animations over JavaScript animations.

Use hardware-accelerated properties whenever possible.

Avoid excessive animation duration.

Avoid continuous animations that consume CPU resources.



Responsive Performance

Responsive layouts should remain efficient across all devices.

Avoid layout shifts.

Prevent unnecessary reflows and repaints.

Maintain smooth scrolling and interaction.



Rendering

Minimize unnecessary rendering work.

Avoid creating complex DOM structures.

Reduce unnecessary element nesting.

Keep page rendering efficient.



Dependencies

Avoid introducing new frontend libraries or frameworks unless explicitly requested.

Before adding any dependency:



Verify that the existing project cannot accomplish the same goal.

Consider the performance impact.

Consider the maintenance cost.



Favor native browser features whenever practical.



Bootstrap Usage

Bootstrap may be used as a layout foundation if already present.

Do not depend on Bootstrap's default visual components.

Avoid loading additional Bootstrap plugins unless required.

Customize the interface rather than relying on default Bootstrap styling.



Network Efficiency

Reduce unnecessary network requests.

Avoid duplicate assets.

Reuse existing resources where appropriate.

Keep resource loading efficient.



Scalability

Design every page assuming the application will continue to grow.

Solutions should remain maintainable even if the project expands significantly.

Avoid implementations that only work for small datasets or a limited number of pages.



Optimization Mindset

Before implementing any feature, consider:



Can it be simpler?

Can it use fewer resources?

Can it reduce network requests?

Can it reduce JavaScript?

Can it improve maintainability?



Always prefer the simplest solution that satisfies the requirements.

7\. Security Guidelines

Security Philosophy

Security is a fundamental requirement.

Never introduce changes that reduce the security of the application.

Preserve all existing security mechanisms unless explicitly instructed otherwise.



Authentication

Do not modify the existing authentication flow.

Preserve:



Login

Logout

Registration

Password Reset

User Sessions



Do not bypass authentication requirements.



Authorization

Never remove or weaken authorization.

Preserve:



Role-based authorization

Policy-based authorization

Access restrictions

Admin-only functionality



Do not expose protected features to unauthorized users.



Anti-Forgery Protection

Preserve all existing anti-forgery protection.

Never remove:



Anti-forgery tokens

Form protection

Existing validation mechanisms



Maintain secure form submission.



Razor Security

Do not bypass Razor's built-in HTML encoding.

Avoid:



Html.Raw()

Rendering untrusted HTML

Injecting user-generated content directly



Only use raw HTML when explicitly required and known to be safe.



User Input

Treat all user input as untrusted.

Do not assume input is valid.

Preserve existing:



Validation

Sanitization

Model validation



Never remove validation for convenience.



Dynamic Data

Always display business data through the existing Razor rendering.

Never expose hidden data in HTML.

Never embed sensitive information inside:



HTML

CSS

JavaScript





Sensitive Information

Never expose or hardcode:



API Keys

Connection Strings

Cloudinary Secrets

Environment Variables

Authentication Tokens

Passwords

Private URLs

Internal Server Information



Sensitive data must remain outside the source code.



Environment Configuration

Assume secrets are managed through environment variables.

Do not move secrets into:



appsettings.json

JavaScript

Razor Views

CSS

GitHub Repository





File Upload Security

Preserve secure file upload behavior.

Do not weaken:



File type validation

File size validation

Image validation



Do not trust uploaded file names.

Assume uploaded files may be malicious.



Client-side Security

Client-side code must never become the source of truth.

Do not move business rules from the backend into JavaScript.

Client-side validation improves usability.

Server-side validation remains authoritative.



Routing Security

Do not expose hidden routes.

Do not generate links to unauthorized pages.

Preserve existing routing behavior.



Error Handling

Do not expose:



Stack traces

Exception details

Internal file paths

Database errors

Server implementation details



Error messages should remain user-friendly.



Dependencies

Do not introduce external libraries without explicit approval.

Every dependency increases:



Security risk

Maintenance cost

Update requirements



Prefer native browser functionality whenever practical.



Security Mindset

Whenever implementing a feature, assume:



Input can be malicious.

Users may manipulate requests.

Client-side code can be modified.

Browser data cannot be trusted.



Never weaken security for convenience.

Always preserve the existing security posture of the application.

8\. Project Standards

General Standards

Every implementation should follow a consistent coding style across the entire project.

Write code as if multiple senior developers will maintain it in the future.

Consistency is more important than personal preference.



Naming Conventions

Use meaningful and descriptive names.

Names should clearly describe their purpose.

Prefer:



ProductCard

FeaturedProducts

HeroSection

NewsletterForm

ProductGallery



Avoid:



Box

Div1

Test

Item

Temp

NewSection

Style1





HTML Standards

Write semantic HTML.

Use appropriate elements whenever possible.

Examples:



header

nav

main

section

article

aside

footer



Avoid excessive div nesting.

Every section should have a clear purpose.



CSS Standards

CSS should remain clean and modular.

Prefer:



Small focused stylesheets

Logical grouping

Consistent spacing

Descriptive class names



Avoid:



!important unless absolutely necessary

Deep selector nesting

Duplicate styles

Inline CSS



Keep CSS easy to maintain.



JavaScript Standards

JavaScript should remain modular.

Avoid:



Global variables

Duplicate functions

Inline JavaScript

Unused code



Keep scripts focused on the page they belong to.



Razor Standards

Keep Razor readable.

Separate presentation from logic.

Do not place business logic inside Views.

Preserve:



Model binding

Tag Helpers

Existing routing

Dynamic rendering





Component Reusability

When functionality is shared between multiple pages:

Prefer reusable:



Partial Views

View Components

Shared Helpers



Avoid duplicated UI implementations.



File Organization

Maintain a predictable folder structure.

Group related files together.

Every page should have clearly organized assets.

Example:

Views/

Products/

Index.cshtml

Details.cshtml

wwwroot/

css/

products/

index.css

details.css

js/

products/

index.js

details.js



Code Simplicity

Prefer simple solutions.

Avoid unnecessary abstraction.

Avoid overengineering.

Write code that another developer can understand quickly.



Maintainability

Every implementation should be easy to:



Read

Modify

Extend

Debug

Review



Avoid clever solutions that reduce readability.



Consistency

When multiple pages implement similar functionality:

Follow the existing project patterns.

Do not create multiple implementations for the same concept.

Maintain visual and structural consistency.



Comments

Write comments only when they provide meaningful value.

Do not explain obvious code.

Prefer self-explanatory code over excessive comments.



Formatting

Maintain consistent formatting throughout the project.

Use consistent:



Indentation

Line spacing

Property ordering

File organization



Avoid inconsistent formatting styles.



Code Duplication

Avoid duplicated:



HTML

CSS

JavaScript

Razor



If duplication becomes noticeable, extract reusable components where appropriate.



Production Quality

Every implementation should be production-ready.

Before considering a task complete, ensure that the code is:



Clean

Modular

Readable

Maintainable

Scalable

Consistent



Do not leave temporary solutions, placeholder code, debugging code, or unfinished implementations.

9\. Final Quality Checklist

Before considering any task complete, verify the following checklist.



Functionality



All existing functionality has been preserved.

No existing features have been broken.

All user interactions continue to work correctly.

Forms continue to submit correctly.

Navigation remains functional.

Existing routes remain unchanged.





Razor \& Dynamic Data



All dynamic data still comes from PostgreSQL (Neon).

No business data has been hardcoded.

Razor rendering remains intact.

Model binding is preserved.

ViewModel binding is preserved.

Tag Helpers remain functional.

Dynamic content has not been replaced with static HTML.





Frontend



The page has been completely redesigned when requested.

The previous UI was not used as a design reference.

The new design follows the Yaqoot Design System.

The logo remains the visual identity reference.

Colors remain consistent with the brand.

Typography is consistent.

Spacing is consistent.

Components follow the established design language.





Architecture



ASP.NET Core MVC architecture has been preserved.

No backend logic has been modified unless explicitly requested.

Controllers remain unchanged.

Models remain unchanged.

Services remain unchanged.

Entity Framework remains unchanged.

Database structure remains unchanged.





CSS



Page-specific styles are isolated.

No unnecessary CSS has been added.

No duplicated styles exist.

Shared CSS contains only shared styles.





JavaScript



JavaScript is only used where necessary.

No unnecessary scripts have been introduced.

Page-specific scripts remain isolated.

Shared scripts remain shared.





Performance



No unnecessary libraries have been added.

The DOM remains clean.

Images are optimized.

Only required assets are loaded.

CSS and JavaScript remain efficient.





Responsive Design

The page has been verified for:



Mobile

Tablet

Laptop

Desktop



No layout issues remain.



Accessibility

Verify:



Semantic HTML

Heading hierarchy

Keyboard accessibility

Readable typography

Adequate color contrast

Clear focus states





Security

Verify that:



No security mechanisms were removed.

Anti-forgery protection remains intact.

Sensitive information is not exposed.

Validation has not been weakened.

No unsafe rendering has been introduced.





Code Quality

Verify that the code is:



Clean

Readable

Modular

Maintainable

Scalable

Production-ready



No dead code remains.

No temporary code remains.

No debugging code remains.



Git

Verify that:



Only the necessary files were modified.

No unrelated files were changed.

Changes remain focused on the requested task.

The task is suitable for a clean Pull Request.





Completion

Before finishing:

Ask yourself:



Is this the best implementation for a production application?

Is the code easy for another developer to maintain?

Does the implementation follow every rule in this document?



If the answer to any of these questions is No, continue improving the implementation before considering the task complete.


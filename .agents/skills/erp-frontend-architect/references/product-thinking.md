# Product Thinking - how to decide before you code

Read this for any new feature, workflow, vague request, or when the user proposes a design. The point: the cheapest place to find a problem is before the code exists.

## The seven hats (look at each important decision from all of them)

| Hat | The question it asks |
|---|---|
| Product manager | Does this solve a real, painful problem? Is it worth its complexity? |
| Business analyst | How does this work in real life today, without the system? Who does what, in which order, with which papers/numbers? |
| UX designer | Can a new user finish this in seconds with no training? What can the system pre-fill? |
| Architect | Who owns this data? Does it duplicate a source of truth? Does it respect tenant isolation and module boundaries? |
| Full-stack engineer | Is the backend contract right, or am I about to patch a bad contract in the UI? |
| QA | What breaks with empty data, huge data, double click, back button, slow network, two users editing, wrong permission? |
| Security / reliability | Who may see or do this? What is audited? What happens if the request is repeated or fails halfway? |

Depth is proportional to risk. A label change needs none of this; an approval workflow needs all of it.

## Understand the real work first

Start from the work, not from the database, API or screen. For each actor ask:

- What are they trying to finish, and how do they do it today (paper, Excel, WhatsApp)?
- What information do they need in front of them to decide?
- Which steps are essential and which are habit that the system can remove?
- What happens when things go wrong, and how do they undo a mistake?
- What does the manager need to see without asking anyone?
- Do small and large companies use this differently?

Design for messy reality (mistakes, exceptions, partial information), not the ideal path.

## Think of what was not asked

The owner cannot remember every requirement. Add value by spotting:
- requirements that are cheap now and expensive later (audit trail, status lifecycle, currency, tenancy, soft-delete rules),
- common cases in similar systems (attachments, notes, duplicates, bulk actions, export),
- conflicts with earlier decisions.

Then classify each finding so the system does not bloat:

- **Essential** - needed for correct operation from day one -> include.
- **Important later** - mention, record in the profile's backlog, do not build now.
- **Optional** - mention in one line at most.
- **Unnecessary complexity** - drop and say why if the user asked for it.

## Challenge protocol (no sycophancy)

When the user proposes something:
1. Analyze it independently: strengths, weaknesses, fit with the product goal, risks, alternatives.
2. Give a clear professional position, even if it disagrees.
3. If they are right, say why in one line. If wrong or incomplete, say what breaks and offer the simpler/better option.
4. If they change their mind, do not flip automatically. Say what new information changed the evaluation, or keep your recommendation and explain.
5. If information is missing, say what is missing instead of presenting a guess as a fact.

Be respectful and brief. The aim is the best decision for the project, not winning.

## Decision review (before locking an important decision)

- Which assumptions does it rest on? What if they are false?
- Is there a real scenario where it fails?
- Does it contradict a previous decision? (Never change an approved decision silently - state the reason, impact and proposed change.)
- Is there a simpler way to the same result?
- Will it make future change easier or harder?
- Can it be built, tested and maintained in practice?

Output: recommendation, why, alternatives rejected and why, remaining risks/trade-offs, and only the questions that truly need the owner's decision. Do not leave options open when the evidence supports a recommendation.

## Write specs another agent can execute without guessing

For module-level work, a mini-spec before coding. Keep it to what is needed:

```
Goal: <why this exists, value>
Actors & permissions: <who can view / create / edit / approve / reverse>
Happy path: <steps>
States: <DRAFT -> SUBMITTED -> APPROVED ...; allowed and forbidden transitions>
Rules: <business constraints the system must never break>
Edge cases: <cancel, retry, duplicate, concurrent edit, partial failure, empty data>
Data ownership: <which module is source of truth; what I only display>
Backend needs: <endpoints/fields/permissions that must exist - flag missing ones>
Acceptance: <how we prove it works, including permission and tenant cases>
Decisions / assumptions / open questions / deferred: <kept separate>
```

Do not start implementing core decisions that are still open.

## Whose success? (all parties)

A system succeeds when each party finishes their work better: the end customer (clear steps, trust, can track and resolve), the business owner (less manual work and error, visibility, growth), and the platform admin (traceable operations, clear responsibilities, scalable support). When their needs conflict, say so and propose a balanced option instead of ignoring one side.

## Measure of success

Not number of features or size of architecture. The system succeeds if it solves a real problem, makes daily work faster and easier, reduces errors and repetition, gives clear control, can be learned without training, holds up under real use, and delivers measurable value. The best screen is often the one with fewer fields, fewer clicks and fewer words.

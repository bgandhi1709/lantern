# Lantern

Lantern is a non-commercial tool that helps parents support their children's school learning, grounded in the school books so answers do not hallucinate.

## Language

### Family and registration

**Family**:
The overarching group that holds Parents and Children, like a resource group in Azure. Its one setting is Region; personal settings belong to each Parent.
_Avoid_: Household, account, user

**Parent**:
A guardian who belongs to a Family and signs in, with their own settings (Language, Consent). A Parent maintains the Family's Children. The POC allows exactly one Parent per Family; later a father or other guardian can join. "Mother" is only the persona used in the docs, not a role in the model.
_Avoid_: User, Admin, Owner, Mother, Father, caregiver

**Child**:
A person in a Family who attends school and whose learning the Parent supports. Never signs in; Lantern serves the Parent, not the Child. "Kid" is only the persona used in the docs.
_Avoid_: Kid, student, learner

**Hard delete**:
How a Child is removed: the Parent decides, and the Child row, the Child's whole Workspace and the Child's History are erased with nothing kept. Lantern finishes it in the background and the Child disappears from the Parent's view at once. The Answer library is never touched by it.
_Avoid_: Archive, soft delete, deactivate

**Family delete**:
How a Family is erased at a Parent's request: its Parents stop being registered at once, then its key, rows, Children and Workspaces are erased with nothing kept. A Parent may register again straight away, as a new Family. The Answer library is never touched by it.
_Avoid_: Close account, deactivate

**Answer library**:
The anonymous store of Scrubbed Questions and tagged Answers that agents build up and Lantern reuses across Families, with no link back to any Child, Parent or Family. It is kept when a Child, Parent or Family is deleted.
_Avoid_: Cache, shared history

**Class**:
A Child's school year, from 1 to 10 for now (for example "Class 6"), as in CBSE and NCERT. Classes 11 and 12 may come later. Only the Parent changes it; Lantern never moves a Child up on its own.
_Avoid_: Grade, standard, level

**Region**:
The Family's city and state, entered as free text. The country is always India.
_Avoid_: Location, address, area

**Consent**:
A Parent's acceptance of a versioned Notice, recorded with a timestamp. Each Parent consents separately; one Parent's Consent does not cover another.
_Avoid_: Agreement, opt-in

**Notice**:
The versioned privacy notice a Parent accepts when giving Consent. Identified by its version string.
_Avoid_: Terms, policy, T&C

**Language**:
The language a Parent reads Lantern in: English, Gujarati or Hindi. A setting of each Parent, not of the Family.
_Avoid_: Locale, medium

**School**:
The name of the school a Child attends, optional free text. It may later become a grouping of Children across Families, with strict separation between Parents.
_Avoid_: Institution, board

### NCERT content

**Subject**:
A field of study a Child learns in a Class, such as Maths or English.
_Avoid_: Course, topic

**Book**:
The NCERT textbook for one Subject in one Class.
_Avoid_: Textbook, volume

**Chapter**:
A numbered unit of a Book, and the unit a Concept belongs to.
_Avoid_: Lesson, unit

**Concept**:
One idea taught in one Chapter of an NCERT book, with a summary, how the book teaches it, its prerequisites and a set of Q&A. The same idea in another Chapter is a separate Concept; linking them is a later layer.
_Avoid_: Topic, skill, node

**Prerequisite**:
Something a Child is expected to know before a Concept, named in plain words. An Answer is always complete and never assumes the Child already knows it.
_Avoid_: Dependency, prior knowledge

**Q&A**:
A pre-written question and answer pair in a Concept, with an optional home activity. One of the ways a Parent asks; it also sets the bounds for what a live Answer may say.
_Avoid_: FAQ, flashcard

**Question**:
What a Parent asks Lantern about their Child's learning, in their own words.
_Avoid_: Query, prompt

**Scrubbed Question**:
A Question with all personal information removed, such as names, School, Region and contact details. It is the only form of a Question that is stored; the raw Question never is.
_Avoid_: Anonymised question, sanitised question

**History**:
The lasting record of a Child's Scrubbed Questions, Tests and Class changes, kept across Class changes for as long as the Child is in Lantern. Each entry carries the Class the Child was in at the time.
_Avoid_: Log, activity, profile

**Workspace**:
A Child's own area in Lantern: one folder per Class, each holding that Class's share of the Child's History and files. Created in the background shortly after the Child is registered or added; moving the Child to a new Class adds a folder. Removing a Child removes its whole Workspace. Every Parent and agent in the Family sees the same Workspaces.
_Avoid_: Class space (its old name), year folder, term, session

**Answer**:
Lantern's reply to a Question, addressed to the Parent and written for the Child's Class.
_Avoid_: Response, result

**Stage**:
A band of Classes from NCF-2023: 1–2, 3–5, 6–8 or 9–10. Internal term, never shown to Parents; it decides which stored Answers a Child can be given.
_Avoid_: Level, band, grade band

### Preparing for school tests

**Test**:
A small school test a Child is preparing for. The Parent names the Chapters of a Subject the test covers, and Lantern helps prepare the Child from them. It replaces photographing book pages and asking a general chatbot.
_Avoid_: Exam, quiz, assessment

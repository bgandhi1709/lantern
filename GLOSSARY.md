# Lantern

Lantern is a non-commercial tool that helps parents support their children's school learning, grounded in the school books so answers do not hallucinate.

## Language

### Family and registration

**Family**:
The overarching group that holds Parents and Children, like a resource group in Azure. Its settings are Region and Board; personal settings belong to each Parent.
_Avoid_: Household, account, user

**Parent**:
A guardian who belongs to a Family and signs in, with their own settings (Language, Consent). A Parent maintains the Family's Children. The POC allows exactly one Parent per Family; later a second Parent (at most two) can join and shares the Family's Recovery code. "Mother" is only the persona used in the docs, not a role in the model.
_Avoid_: User, Admin, Owner, Mother, Father, caregiver

**Locked value**:
A personal detail the phone has locked with the Family key before sending it, so Lantern stores and returns it without being able to read it. The Parent's name and email and each Child's name, birth year and School are locked values; the API keeps their plain names, and only the content is locked.
_Avoid_: Encrypted field (the server does not encrypt), ciphertext

**Recovery code**:
The one secret that opens a Family, made by the phone and shown once at registration. The phone uses it to wrap the Family key, so Lantern holds no key and cannot read the locked values. The Parent must email it to their own address from the phone's mail app before registration continues (the share sheet is the fallback); Lantern never sends or sees it. A Parent who loses it and the phone starts the Family again as a new Family. There is no Passphrase (ADR-0015).
_Avoid_: Passphrase, backup key, reset code

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
A Child's school year (for example "Class 6"): 1 to 10 for a CBSE Family, 1 to 5 for an SSC Family for now. Classes 11 and 12 may come later. Only the Parent changes it; Lantern never moves a Child up on its own.
_Avoid_: Grade, standard, level

**Board**:
The school board whose syllabus and Books a Family follows: CBSE (NCERT Books) or SSC, the Maharashtra State Board (Balbharati Books, English medium). A setting of the Family, picked at registration and fixed for the pilot; every Child in the Family follows it. A wrong pick is fixed by a Family delete and registering again.
_Avoid_: Curriculum, syllabus, medium

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

### Book content

**Subject**:
A field of study a Child learns in a Class, such as Maths or English.
_Avoid_: Course, topic

**Book**:
The textbook a Board prescribes for one Subject in one Class: NCERT for CBSE, Balbharati (English medium) for SSC.
_Avoid_: Textbook, volume

**Book guidance**:
The rights holder's own notes for teachers and parents in a Book: in its front matter and in boxes beside its lessons. Lantern's agents read it as context for a Plan or an Answer, never as instructions.
_Avoid_: Teacher notes, preface, instructions

**Board profile**:
How a Board's Books are built: what a Chapter is (an English Balbharati Unit, an NCERT chapter), how lessons nest, where Book guidance sits. Agents pick their prompt by Board using it. Internal term.
_Avoid_: Schema, template

**Content slice**:
The copy of a Board's Books for one Child's Class, kept inside that Class's folder of the Child's Workspace. Plan, Ask and Check read it and never the shared Board container, so a Family needs nothing outside its own storage. It is made in the background when the Child is registered or changes Class, is ready once its `version.json` exists, and stays pinned to the version it was copied from until a refresh. Only the runtime JSON is copied.
_Avoid_: Cache, snapshot, local copy

**Board profile**:
A Board's own `board.json` in its master container: how its Books are built and shown, such as the order and icon of its Subjects and where its Chapter titles come from. Read by the build when it makes the Class manifests, never by the API.
_Avoid_: Board config, Board rules file

**Class manifest**:
The ready-made list of one Board's Subjects and Chapters for one Class, made once when the content is built and copied into each Child's Content slice. Every Board has the same shape, so the app never needs to know which Board it is showing.
_Avoid_: Class index, Subject list

**Family manifest**:
One file in the Family's own storage listing each Child whose Content slice is ready, with the Child's Class and Subjects. It is derived from the slices and never the record of readiness, so it can always be rebuilt. The API reads it to show Home without opening each slice; only the background work writes it, when a Child is added or removed.
_Avoid_: Cache, Family index, Child list

**Chapter**:
A numbered unit of a Book, and the unit a Concept belongs to. The app lists a Subject's Chapters numbered 1 to N across all its Books, in book order, not by each Book's own printed numbers.
_Avoid_: Lesson, unit

**Concept**:
One idea taught in one Chapter of a Book, with a summary, how the book teaches it, its prerequisites and a set of Q&A. The same idea in another Chapter is a separate Concept; linking them is a later layer.
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
The lasting record of a Child's Scrubbed Questions, Plan progress, Checks, Tests and Class changes, kept across Class changes for as long as the Child is in Lantern. Each entry carries the Class the Child was in at the time.
_Avoid_: Log, activity, profile

**Workspace**:
A Child's own area in Lantern: one folder per Class, each holding that Class's share of the Child's History and files, and its Content slice. Created in the background shortly after the Child is registered or added; moving the Child to a new Class adds a folder. Removing a Child removes its whole Workspace. Every Parent and agent in the Family sees the same Workspaces.
_Avoid_: Class space (its old name), year folder, term, session

**Answer**:
Lantern's reply to a Question, addressed to the Parent and written for the Child's Class.
_Avoid_: Response, result

**Stage**:
A band of Classes from NCF-2023: 1–2, 3–5, 6–8 or 9–10. Internal term, never shown to Parents; it decides which stored Answers a Child can be given.
_Avoid_: Level, band, grade band

### Teaching a Chapter

**Plan**:
Lantern's guide for teaching one Chapter, built from the Chapter's Concepts in book order: for each Concept, its summary, how the book teaches it, a home activity and a Recall. Ends with a Check. Progress is kept per Concept, so a Parent can stop and resume.
_Avoid_: Lesson plan, session plan, schedule

**Chapter summary**:
A few plain lines on what a Chapter is about, shown before the Parent opens its Plan. Written once, offline, for every Chapter.
_Avoid_: Overview, abstract

**Recall**:
One or two quick questions the Parent asks right after a Concept, on what was just taught, to keep the Child engaged. Not scored.
_Avoid_: Quiz, mini quiz, Check

**Check**:
The Parent's assessment, at the end of a Chapter, of whether the teaching landed. Questions are drawn from across the Chapter's Concepts, never repeating a Recall; the Parent asks each aloud and marks it right or wrong. Each question keeps its Concept, so a Check scores the Chapter and each Concept. A Chapter can be checked again; every Check is kept.
_Avoid_: Test, quiz, exam, assessment

**Weak Concept**:
A Concept the Child got wrong in the Chapter's latest Check.
_Avoid_: Gap, weakness

**General question**:
A Question about something outside the open Chapter. Logged and not yet answered.
_Avoid_: Off-topic question, unbounded question

### Preparing for school tests

**Test**:
A small school test a Child is preparing for. The Parent names the Chapters of a Subject the test covers, and Lantern helps prepare the Child from them. It replaces photographing book pages and asking a general chatbot. Not the Check, which Lantern sets after a Plan.
_Avoid_: Exam, quiz, assessment, Check

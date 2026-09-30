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

**Class**:
A Child's school year, from 1 to 10 for now (for example "Class 6"), as in CBSE and NCERT. Classes 11 and 12 may come later.
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

**Answer**:
Lantern's reply to a Question, addressed to the Parent and written for the Child's Class.
_Avoid_: Response, result

**Stage**:
A band of Classes from NCF-2023: 1–2, 3–5, 6–8 or 9–10. Internal term, never shown to Parents; it decides which stored Answers a Child can be given.
_Avoid_: Level, band, grade band

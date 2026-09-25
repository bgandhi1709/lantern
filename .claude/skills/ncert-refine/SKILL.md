---
name: ncert-refine
description: Use when asked to refine an NCERT chapter (an id like aejm101) into concept cards and parent Q&A for Lantern, or when running /ncert-refine.
---

# ncert-refine

One chapter in, one JSON file out. Everything needed is in the bundle and this page.

## Steps

1. Read `../lantern-data/bundles/<book>/<id>.md`, where book = id without its last two digits
   (`aejm101` → `aejm1`). Read nothing else, with one exception: a `[pN picture …]` block is a
   local model's description of a page drawn rather than written. Open the image it names only
   if the description is unclear or garbled on something you need.
2. Write the JSON below with one Write, to the `write to:` path on the bundle's third line.
3. Run `tools/ncert-build/.venv/bin/ncert-build check <id>`. If it lists problems, fix only those
   with Edit, then run it once more.
4. Reply with the check's line only, e.g. `aejm101: ok, 4 concepts, 24 Q&A`.

## Output: refine-v1

```json
{
  "schema_version": "refine-v1",
  "chapter_id": "aejm101",
  "chapter_title": "Finding the Furry Cat!",
  "model": "<your model id>",
  "concepts": [
    {
      "id": "aejm101-c1",
      "name": "Inside and outside",
      "summary": "Words that tell where a thing is: inside, outside, above, below.",
      "how_taught": "A poem about a hiding cat, then a game putting a ball in and out of a box.",
      "key_terms": ["inside", "outside"],
      "prerequisites": [],
      "pages": [1, 2, 3],
      "qa": [
        {"q": "Where is the cat hiding?", "a": "Under the bed. Under means it is below the bed, close to it.", "try": "Hide a spoon under a cup. Ask where it is."}
      ]
    }
  ]
}
```

## Content

- **Source:** facts come only from the chapter text and its picture descriptions. The draft is a
  hint list and can be wrong; where they disagree, the text wins. Use the numbers and objects a
  picture description gives, not invented examples.
- **Concepts:** 2–8, one per idea. Merge draft items that are the same idea taught through
  different activities. Ids run `<id>-c1`, `-c2`, … in order.
- **pages:** chapter page numbers from the `[pN]` markers, picture blocks included.
- **how_taught:** the book's own method, so the mother teaches it the same way. At most 40 words.
- **prerequisites:** what the child learned before this chapter, as plain names. Use an empty list
  for Class 1 or when the chapter shows nothing.
- **qa:** 5–8 per concept.
  - `q`: how a child of the bundle's class would ask it.
  - `a`: what the mother can say to the child, in words that child knows. Short sentences.
    At most 60 words. Plain text.
  - `try`: a one-line activity with things at home, when one fits. Leave the key out otherwise.
- **summary:** at most 40 words, for the mother.

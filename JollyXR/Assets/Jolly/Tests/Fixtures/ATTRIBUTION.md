# Test fixture provenance

## images/

| File | Source | Author | Licence |
|---|---|---|---|
| `pen_640.jpg` | "04-BICcristal2008-03-26.jpg", https://commons.wikimedia.org/wiki/File:04-BICcristal2008-03-26.jpg, downscaled to 640×428 | Trounce | [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) |
| `mug_640.jpg` | "Coffee Mug (Unsplash).jpg", https://commons.wikimedia.org/wiki/File:Coffee_Mug_(Unsplash).jpg, downscaled to 640×360 | Ryan Holloway | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |

Copied from `docs/research/samples/` (`docs/research/meta-model-api.md` §9).

## sam/

Responses recorded from live `sam-3.1` calls on 2026-11-01 (research phase, `docs/research/meta-model-api.md` §4), with no
edits other than dropping the SSE framing:

| File | Request |
|---|---|
| `pen_concept_onebit.response.json` | concept `"pen"` on `pen_640.jpg`, `include_confidence:"true"`, `mask_encoding:"one_bit"`; the `response.completed` payload of the stream |
| `mug_concept_onebit.response.json` | concept `"mug"` on `mug_640.jpg`, `mask_encoding:"one_bit"`, non-streaming |
| `mug_banana_nomatch.response.json` | concept `"banana"` on the 1280 px mug photo: zero matches, still one billed image |
| `pen_point_box_lossless.txt` | output text only: object 1 = point (470, 300), object 2 = the pen box, on `pen_640.jpg`, without `mask_encoding` (lossless `~` masks) |

The masks are Meta API output about the CC BY / CC0 images above. They contain no personal data.

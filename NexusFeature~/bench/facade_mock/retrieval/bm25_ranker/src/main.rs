// Mirrors codex-rs/core/src/tools/handlers/tool_search.rs (rust-v0.160.0):
//   SearchEngineBuilder::<usize>::with_documents(Language::English, docs).build(); engine.search(query, limit)
// stdin: {"docs":[str], "queries":[str]}  stdout: {"results":[[[id,score],...]], "doc_tokens":[[..]], "query_tokens":[[..]], "avgdl":f}
use bm25::{Document, Language, SearchEngineBuilder, DefaultTokenizer, Tokenizer};
use std::io::Read;
fn main() {
    let mut s = String::new();
    std::io::stdin().read_to_string(&mut s).unwrap();
    let v: serde_json::Value = serde_json::from_str(&s).unwrap();
    let docs: Vec<String> = v["docs"].as_array().unwrap().iter().map(|x| x.as_str().unwrap().to_string()).collect();
    let queries: Vec<String> = v["queries"].as_array().unwrap().iter().map(|x| x.as_str().unwrap().to_string()).collect();
    let documents: Vec<Document<usize>> = docs.iter().cloned().enumerate().map(|(i, t)| Document::new(i, t)).collect();
    let engine = SearchEngineBuilder::<usize>::with_documents(Language::English, documents).build();
    let tok = DefaultTokenizer::new(Language::English);
    let results: Vec<Vec<(usize, f32)>> = queries.iter().map(|q| {
        engine.search(q.as_str(), None).into_iter().map(|r| (r.document.id, r.score)).collect()
    }).collect();
    let out = serde_json::json!({
        "results": results,
        "doc_tokens": docs.iter().map(|d| tok.tokenize(d)).collect::<Vec<_>>(),
        "query_tokens": queries.iter().map(|q| tok.tokenize(q)).collect::<Vec<_>>(),
    });
    println!("{}", out);
}

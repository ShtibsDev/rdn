use criterion::{black_box, criterion_group, criterion_main, Criterion};

fn parse_benchmark(c: &mut Criterion) {
    let simple_json = r#"{"name": "test", "value": 42}"#;
    let rdn_with_types = r#"{"date": @2024-01-15T10:30:00.000Z, "id": 42n, "tags": Set{"a", "b"}}"#;

    c.bench_function("parse_simple_json", |b| {
        b.iter(|| {
            // TODO: Uncomment when parser is implemented
            // rdn::parse(black_box(simple_json)).unwrap()
            black_box(simple_json);
        })
    });

    c.bench_function("parse_rdn_extended", |b| {
        b.iter(|| {
            // TODO: Uncomment when parser is implemented
            // rdn::parse(black_box(rdn_with_types)).unwrap()
            black_box(rdn_with_types);
        })
    });
}

criterion_group!(benches, parse_benchmark);
criterion_main!(benches);

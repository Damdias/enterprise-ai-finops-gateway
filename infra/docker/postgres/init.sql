
CREATE TABLE IF NOT EXISTS document_jobs (
    id UUID PRIMARY KEY,
    filename VARCHAR(255) NOT NULL,
    status VARCHAR(30) NOT NULL DEFAULT 'queued',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    error_message TEXT,
    CONSTRAINT document_jobs_status_check
        CHECK (status IN (
            'queued',
            'processing',
            'completed',
            'failed'
        ))
);

CREATE INDEX IF NOT EXISTS idx_document_jobs_status
    ON document_jobs(status);

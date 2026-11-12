import json
import logging
import os
import time

import pika
import psycopg

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("document-worker")

DATABASE_URL = os.environ["DATABASE_URL"]
RABBITMQ_HOST = os.environ["RABBITMQ_HOST"]
RABBITMQ_USER = os.environ["RABBITMQ_USER"]
RABBITMQ_PASSWORD = os.environ["RABBITMQ_PASSWORD"]

QUEUE_NAME = "document_jobs"


def process_job(job: dict):
    job_id = job["id"]
    filename = job["filename"]

    logger.info("Processing job %s: %s", job_id, filename)

    with psycopg.connect(DATABASE_URL) as conn:
        with conn.cursor() as cur:
            cur.execute(
                """
                UPDATE document_jobs
                SET status = 'processing',
                    started_at = NOW()
                WHERE id = %s
                  AND status <> 'completed'
                """,
                (job_id,),
            )

            # Ignore an already completed or unknown job.
            if cur.rowcount == 0:
                logger.info("Skipping job %s", job_id)
                return

        conn.commit()

        # Simulated document processing.
        # Real PDF extraction comes in the next phase.
        time.sleep(2)

        with conn.cursor() as cur:
            cur.execute(
                """
                UPDATE document_jobs
                SET status = 'completed',
                    completed_at = NOW(),
                    error_message = NULL
                WHERE id = %s
                """,
                (job_id,),
            )

        conn.commit()

    logger.info("Completed job %s", job_id)


def main():
    credentials = pika.PlainCredentials(RABBITMQ_USER, RABBITMQ_PASSWORD)

    parameters = pika.ConnectionParameters(
        host=RABBITMQ_HOST, credentials=credentials, heartbeat=60
    )

    while True:
        try:
            connection = pika.BlockingConnection(parameters)
            channel = connection.channel()

            channel.queue_declare(queue=QUEUE_NAME, durable=True)

            channel.basic_qos(prefetch_count=1)

            def on_message(ch, method, properties, body):
                try:
                    job = json.loads(body)
                    process_job(job)

                    # Acknowledge only after DB commit.
                    ch.basic_ack(delivery_tag=method.delivery_tag)

                except Exception:
                    logger.exception("Job processing failed")

                    # Simplified development behavior.
                    # A production worker should use
                    # bounded retries and a dead-letter queue.
                    ch.basic_nack(delivery_tag=method.delivery_tag, requeue=False)

            channel.basic_consume(queue=QUEUE_NAME, on_message_callback=on_message)

            logger.info("Worker waiting for messages...")
            channel.start_consuming()

        except (pika.exceptions.AMQPError, OSError):
            logger.exception("RabbitMQ unavailable; reconnecting")
            time.sleep(5)


if __name__ == "__main__":
    main()

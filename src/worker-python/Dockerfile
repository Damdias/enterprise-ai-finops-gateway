
FROM python:3.13-slim

WORKDIR /app

COPY requirements.txt .

RUN pip install --no-cache-dir -r requirements.txt

COPY src/worker_python/worker.py .

RUN useradd --create-home --uid 10001 appuser

USER appuser

CMD ["python", "-u", "worker.py"]
